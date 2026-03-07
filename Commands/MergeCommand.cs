using System.Threading.Channels;

using Microsoft.IO;

using NaturalSort.Extension;

using RanaPdfTool.Models;
using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class MergeCommand(
    RecyclableMemoryStreamManager rmsManager,
    IPdfService pdfService,
    IImageService imageService
) : AsyncCommand<MergeSettings>
{
    private readonly RecyclableMemoryStreamManager _rmsManager = rmsManager;
    private readonly IPdfService _pdfService = pdfService;
    private readonly IImageService _imageService = imageService;

    private static readonly int _cpuCount = Math.Max(1, Environment.ProcessorCount - 1); // 保留一个核心给系统和UI
    private static readonly int _boundedCapacity = Math.Clamp(_cpuCount * 2, 10, 50); // 并行通道的容量，防止OOM

    // --- DTO定义 ---
    // 1. 待加载任务：包含文件位置信息
    private sealed record LoadJob(
        IList<string> ParentList,
        int Index,
        string FilePath);

    // 2. 待处理任务：包含已加载到内存的数据流
    private sealed record ProcessJob(
        IList<string> ParentList,
        int Index,
        string FilePath,
        MemoryStream InputStream);

    // 3. 待保存任务：包含处理后的数据流
    private sealed record SaveJob(
        IList<string> ParentList,
        int Index,
        string OriginalPath,
        MemoryStream OutputStream);

    // 4. 处理结果：用于 UI 更新和列表回写
    private sealed record WorkResult(
        IList<string> ParentList,
        int Index,
        string OriginalPath,
        string? NewPath, // 如果发生了转换，这里是新路径；否则和 OriginalPath 相同
        bool Success,
        Exception? Exception);

    public override async Task<int> ExecuteAsync(CommandContext context, MergeSettings settings, CancellationToken cancellationToken)
    {
        int jpgQuality = settings.Quality ?? 90;

        // 1. 解析并验证源路径 (Source)
        var (sourceOk, sourceDir) = CliGuard.TryRun<string, ArgumentException>(
            () => PathHelper.ResolveAbsolutePath(settings.SourceDir),
            $"Invalid source path: {settings.SourceDir}");

        if (!sourceOk || string.IsNullOrEmpty(sourceDir))
            return 1;

        if (!Directory.Exists(sourceDir))
        {
            StdErr.MarkupLine($"[red][[ERROR]][/] Source directory not found: [red underline]{Markup.Escape(sourceDir)}[/]");
            return 1;
        }

        // 2. 解析目标路径 (Destination)
        var (destOk, rawDestPath) = CliGuard.TryRun<string, ArgumentException>(
            () => PathHelper.ResolveAbsolutePath(settings.DestDir),
            $"Invalid destination path: {settings.DestDir}");

        if (!destOk || string.IsNullOrEmpty(rawDestPath))
            return 1;

        string finalPdfPath = string.Empty;

        // 3. 核心判断逻辑
        // 只有当输入以 .pdf 结尾时，才视为用户指定了具体文件名
        bool isExplicitFile = rawDestPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        if (isExplicitFile)
        {
            // --- 场景 A: 用户指定了文件名 (e.g. ./Output/book.pdf) ---

            // 边界检查：检查是否存在与目标文件“同名”的文件夹
            if (Directory.Exists(rawDestPath))
            {
                StdErr.MarkupLine($"[red][[ERROR]][/] Cannot create file [red underline]{Markup.Escape(Path.GetFileName(rawDestPath))}[/]. A folder with the same name already exists at destination.");
                return 1;
            }

            // 确保父目录存在
            string? parentDir = Path.GetDirectoryName(rawDestPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                try
                {
                    _ = Directory.CreateDirectory(parentDir);
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                    return 1;
                }
            }

            finalPdfPath = rawDestPath;
        }
        else
        {
            // --- 场景 B: 用户仅指定了目录 (e.g. ./Output 或 ./Output/mybook) ---

            // 即使写了后缀，只要没写 .pdf，我们就认为是目录，但是应当提示用户
            if (Path.HasExtension(rawDestPath))
            {
                string ext = Path.GetExtension(rawDestPath);
                AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] The input '[yellow]{Markup.Escape(ext)}[/]' is not a [blue].pdf[/] extension.");
                AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] The path will be treated as a [bold]directory[/], and the PDF will be generated inside it.");
            }

            // 边界检查：检查该路径是否已经是一个“文件”了
            if (File.Exists(rawDestPath))
            {
                StdErr.MarkupLine($"[red][[ERROR]][/] Destination path [red underline]{Markup.Escape(rawDestPath)}[/] exists and is a file. Please specify a directory or a new .pdf filename.");
                return 1;
            }

            // 如果目录不存在，尝试创建
            if (!Directory.Exists(rawDestPath))
            {
                bool createDirOk = CliGuard.TryRun<Exception>(
                    () => Directory.CreateDirectory(rawDestPath),
                    $"Failed to create destination directory: {rawDestPath}. Check permissions.");

                if (!createDirOk)
                    return 1;
            }

            // 计算文件名：默认为源文件夹名称
            var dirInfo = new DirectoryInfo(sourceDir);
            // 处理根目录 (如 C:\) Name 为空的情况，改用 Root ，并处理 C:\ -> C
            string sourceFolderName = string.IsNullOrWhiteSpace(dirInfo.Name)
                ? dirInfo.Root.Name
                    .Replace(Path.VolumeSeparatorChar.ToString(), "")
                    .Replace(Path.DirectorySeparatorChar.ToString(), "")
                : dirInfo.Name;

            // 兜底：如果还为空，使用默认名
            if (string.IsNullOrWhiteSpace(sourceFolderName))
                sourceFolderName = "output";

            finalPdfPath = Path.Combine(rawDestPath, $"{sourceFolderName}.pdf");
        }

        // 4. 自动重命名 (避免覆盖已有文件)
        // 使用 TryRun 包裹，虽然主要是计算字符串，但涉及 File.Exists IO操作
        var (uniqueOk, uniquePath) = CliGuard.TryRun<string, IOException>(
            () => PathHelper.GetUniqueFilePath(finalPdfPath),
            "Failed to generate a unique filename. Access to the path might be denied.");

        if (!uniqueOk || string.IsNullOrEmpty(uniquePath))
            return 1;

        finalPdfPath = uniquePath;
        string finalPdfLink = MarkupHelper.FileLinkMarkup(finalPdfPath);

        // 配置自然排序比较器
        var naturalComparer = StringComparer.OrdinalIgnoreCase.WithNaturalSort();

        AnsiConsole.MarkupLine("[gray]Scanning directory structure...[/]");

        DirectoryNode rootNode;
        try
        {
            rootNode = NodeHelper.BuildDirectoryTree(new DirectoryInfo(sourceDir), naturalComparer);
        }
        catch (Exception ex)
        {
            StdErr.MarkupLine($"[red][[ERROR]][/] [white]Unexpected Error Happened:[/]");
            StdErr.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }

        int totalFiles = rootNode.TotalFileCount();

        if (totalFiles == 0)
        {
            AnsiConsole.MarkupLine("[yellow][[WARNING]][/] No images found in source directory.");
            return 0;
        }

        var tempFiles = new List<string>();
        var errors = new List<(string context, Exception exception)>();
        bool hasCriticalFailure = false;

        try
        {
            await AnsiConsole
                .Progress()
                .AutoClear(false)
                .Columns([
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn
                    {
                        CompletedStyle = new Style(ColorHelper.ProcessingAccentColor),
                        FinishedStyle = new Style(ColorHelper.FinishedAccentColor)
                    },
                    new PercentageColumn
                    {
                        CompletedStyle = new Style(ColorHelper.FinishedAccentColor),
                    },
                    new SpinnerColumn
                    {
                        Style = new Style(ColorHelper.ProcessingAccentColor),
                    },
                    new ElapsedTimeColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    // --- 阶段 1: 预处理图片 ---
                    var prepTask = ctx.AddTask("[green]Processing images...[/]");

                    // --- 1. 准备数据源 ---

                    var allFileLists = NodeHelper
                        .GetAllFileLists(rootNode)
                        .ToList();
                    // 扁平化所有文件项，方便分发
                    var rawItems = allFileLists
                        .SelectMany(list => list
                            .Select((file, index)
                                => new LoadJob(list, index, file)))
                        .ToList();

                    prepTask.MaxValue = rawItems.Count;

                    // --- 2. 创建流水线通道 ---

                    // [Channel 1] LoadChannel: 待读取的文件路径
                    var loadChannel = Channel.CreateBounded<LoadJob>(
                        new BoundedChannelOptions(1000)
                        {
                            SingleWriter = true,
                            SingleReader = true
                        });

                    // [Channel 2] ProcessChannel: 待处理的内存流
                    var processChannel = Channel.CreateBounded<ProcessJob>(
                        new BoundedChannelOptions(_boundedCapacity)
                        {
                            SingleWriter = true,
                            SingleReader = false
                        });

                    // [Channel 3] SaveChannel: 待写入的内存流
                    var saveChannel = Channel.CreateBounded<SaveJob>(
                        new BoundedChannelOptions(_boundedCapacity * 2)
                        {
                            SingleWriter = false,
                            SingleReader = true
                        });

                    // [Channel 4] ResultChannel: 结果汇总
                    var resultChannel = Channel.CreateUnbounded<WorkResult>(
                        new UnboundedChannelOptions
                        {
                            SingleWriter = false,
                            SingleReader = true
                        });

                    // --- 3. 装配流水线任务 ---

                    // A. Coordinator: UI 刷新、列表更新、错误收集
                    var coordinatorTask = Task.Run(async () =>
                    {
                        await foreach (var result in resultChannel.Reader.ReadAllAsync())
                        {
                            if (result.Success)
                            {
                                // 只有路径变了才更新 (PNG -> JPG)
                                if (result.NewPath != result.OriginalPath)
                                {
                                    result.ParentList[result.Index] = result.NewPath!;
                                    tempFiles.Add(result.NewPath!);
                                }
                            }
                            else
                            {
                                errors.Add((Path.GetFileName(result.OriginalPath), result.Exception!));
                                result.ParentList[result.Index] = null!; // 标记为无效
                            }
                            prepTask.Increment(1);
                        }
                    });

                    // B. Saver: 从内存流写入磁盘，释放内存流
                    var saverTask = Task.Run(async () =>
                    {
                        await foreach (var job in saveChannel.Reader.ReadAllAsync())
                        {
                            try
                            {
                                // 生成临时文件路径
                                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");

                                // IO 操作：写入磁盘
                                job.OutputStream.Position = 0;
                                using (var fs = new FileStream(
                                    tempPath,
                                    FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.None,
                                    4096,
                                    useAsync: true))
                                {
                                    await job.OutputStream.CopyToAsync(fs);
                                }

                                // 发送成功结果
                                await resultChannel.Writer.WriteAsync(
                                new WorkResult(
                                    job.ParentList,
                                    job.Index,
                                    job.OriginalPath,
                                    tempPath,
                                    true,
                                    null));
                            }
                            catch (Exception ex)
                            {
                                await resultChannel.Writer.WriteAsync(
                                new WorkResult(
                                    job.ParentList,
                                    job.Index,
                                    job.OriginalPath,
                                    null,
                                    false,
                                    ex));
                            }
                            finally
                            {
                                // 释放 Process 阶段产生的内存流
                                await job.OutputStream.DisposeAsync();
                            }
                        }
                    });

                    // C. Processor: 解码图片，编码为 JPEG，不涉及磁盘 IO
                    var processorTasks = new Task[_cpuCount];
                    for (int i = 0; i < _cpuCount; i++)
                    {
                        processorTasks[i] = Task.Run(async () =>
                        {
                            await foreach (var job in processChannel.Reader.ReadAllAsync())
                            {
                                // 准备输出流
                                var outStream = _rmsManager.GetStream("ProcessorOutput");
                                try
                                {
                                    // CPU 操作
                                    _imageService.ConvertPngToTempJpegStream(
                                        job.InputStream,
                                        outStream,
                                        jpgQuality);

                                    // 发送给 Saver
                                    await saveChannel.Writer.WriteAsync(
                                        new SaveJob(
                                            job.ParentList,
                                            job.Index,
                                            job.FilePath,
                                            outStream));
                                }
                                catch (Exception ex)
                                {
                                    // 如果处理失败，直接发给结果通道，跳过 Saver
                                    await outStream.DisposeAsync(); // 清理没用上的输出流
                                    await resultChannel.Writer.WriteAsync(
                                        new WorkResult(
                                            job.ParentList,
                                            job.Index,
                                            job.FilePath,
                                            null,
                                            false,
                                            ex));
                                }
                                finally
                                {
                                    // 关键：释放 Load 阶段产生的输入流
                                    await job.InputStream.DisposeAsync();
                                }
                            }
                        });
                    }

                    // D. Loader: 读取磁盘文件到内存流
                    var loaderTask = Task.Run(async () =>
                    {
                        await foreach (var job in loadChannel.Reader.ReadAllAsync())
                        {
                            var memoryStream = _rmsManager.GetStream("LoaderInput");
                            try
                            {
                                // IO 操作
                                // 直接将文件流 Copy 到池化的 MemoryStream
                                using (var fs = new FileStream(
                                    job.FilePath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.Read,
                                    4096,
                                    useAsync: true))
                                {
                                    await fs.CopyToAsync(memoryStream);
                                }
                                memoryStream.Position = 0; // 重置位置以便 Processor 读取
                                
                                // 发送给 Processor
                                await processChannel.Writer.WriteAsync(
                                new ProcessJob(
                                    job.ParentList,
                                    job.Index,
                                    job.FilePath,
                                    memoryStream));
                            }
                            catch (Exception ex)
                            {
                                // 如果加载失败，跳过中间步骤，直接报告错误
                                await memoryStream.DisposeAsync(); // 清理没用上的内存流
                                await resultChannel.Writer.WriteAsync(
                                new WorkResult(
                                    job.ParentList,
                                    job.Index,
                                    job.FilePath,
                                    null,
                                    false,
                                    ex));
                            }
                        }
                    });

                    // --- 4. 生产者分发 + 关闭流水线 ---

                    try
                    {
                        foreach (var item in rawItems)
                        {
                            string ext = Path.GetExtension(item.FilePath);
                            bool needsConversion = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && !settings.Raw;

                            if (needsConversion)
                            {
                                // 需要处理的任务 -> 进入 LoadChannel
                                await loadChannel.Writer.WriteAsync(item, cancellationToken);
                            }
                            else
                            {
                                // 不需要处理 -> 直接进入 ResultChannel
                                await resultChannel.Writer.WriteAsync(
                                    new WorkResult(item.ParentList,
                                    item.Index,
                                    item.FilePath,
                                    item.FilePath,
                                    true,
                                    null));
                            }
                        }
                    }
                    finally
                    {
                        loadChannel.Writer.Complete(); // D. 通知 Loader 不再接收新任务
                    }

                    // --- 5. 等待任务完成 + 关闭流水线 ---

                    try
                    {
                        await loaderTask; // 等待所有文件加载进内存
                    }
                    finally
                    {
                        processChannel.Writer.Complete(); // C. 通知 Processors 不再接收新任务
                    }

                    try
                    {
                        await Task.WhenAll(processorTasks); // 等待所有内存图片转换完成
                    }
                    finally
                    {
                        saveChannel.Writer.Complete(); // B. 通知 Saver 不再接收新任务
                    }

                    try
                    {
                        await saverTask; // 等待所有结果写入磁盘
                    }
                    finally
                    {
                        resultChannel.Writer.Complete(); // A. 通知 Coordinator 不再接收新任务
                    }

                    await coordinatorTask; // 等待所有 UI 更新完成

                    // --- 6. 数据清理 ---

                    // 统一清理处理失败的项 (null)
                    foreach (var fileList in allFileLists)
                        _ = fileList.RemoveAll(f => f == null);

                    // 将任务标记为完成，并停止UI刷新
                    prepTask.Value = prepTask.MaxValue;
                    prepTask.StopTask();

                    // 重新计算有效文件数
                    int validFileCount = rootNode.TotalFileCount();
                    if (validFileCount == 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] No valid files found for PDF generation.");
                        return;
                    }

                    // --- 阶段 2: 生成 PDF ---
                    var mergeTask = ctx.AddTask("[green]Generating PDF...[/]", maxValue: validFileCount);

                    await Task.Run(() =>
                        _pdfService.MergeImagesToPdf(
                            rootNode, // 传入树根节点
                            finalPdfPath,
                            settings.Resize,
                            onProgress: () => mergeTask.Increment(1),
                            onItemError: (fileName, ex) => errors.Add((fileName, ex))
                        ));

                    mergeTask.Value = mergeTask.MaxValue;
                    mergeTask.StopTask();
                });
        }
        catch (Exception ex)
        {
            errors.Add(("CRITICAL EXECUTION ERROR", ex));
            hasCriticalFailure = true;
        }
        finally
        {
            // 清理临时文件
            if (tempFiles.Count != 0)
            {
                AnsiConsole.Status().Start("Cleaning up...", _ =>
                {
                    foreach (string temp in tempFiles)
                    {
                        if (File.Exists(temp))
                            File.Delete(temp);
                    }
                });
            }
        }

        // --- 结果汇总 ---
        if (errors.Count == 0)
        {
            AnsiConsole.MarkupLine($"[green][[SUCCESS]][/] Successfully created: [green underline]{finalPdfLink}[/]");
            return 0;
        }
        else
        {
            // 如果生成了部分文件，提示位置
            if (File.Exists(finalPdfPath))
                AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] PDF created with warnings at: {finalPdfLink}");

            StdErr.MarkupLine($"[red][[ERROR]][/] Completed with [red bold]{errors.Count}[/] errors.");
            if (hasCriticalFailure)
                StdErr.MarkupLine("[red][[ERROR]][/] Including [bold]CRITICAL ERROR[/].");

            StdErr.Write(new Rule("[red]Merge Failures[/]").LeftJustified());
            foreach (var (ctxName, exception) in errors)
            {
                StdErr.MarkupLine($"[gray bold]Item:[/] [underline]{Markup.Escape(ctxName)}[/]");
                StdErr.WriteException(exception, ExceptionFormats.ShortenEverything);
                StdErr.WriteLine();
            }
            return 1;
        }
    }
}
