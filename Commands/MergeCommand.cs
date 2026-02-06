using System.Threading.Channels;

using NaturalSort.Extension;

using RanaPdfTool.Models;
using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class MergeCommand(IPdfService pdfService, IImageService imageService) : AsyncCommand<MergeSettings>
{
    private readonly IPdfService _pdfService = pdfService;
    private readonly IImageService _imageService = imageService;

    // 用于并行图像处理阶段的任务描述结构
    private readonly record struct ImageJob(
        IList<string> ParentList,
        int Index,
        string FilePath);
    // 用于并行图像处理阶段的结果传递结构，包含必要的信息以便后续更新树结构和错误记录
    private readonly record struct ImageResult(
        IList<string> ParentList,
        int Index,
        string OriginalPath,
        string? NewPath,
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
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Source directory not found - [underline]{Markup.Escape(sourceDir)}[/]");
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
                AnsiConsole.MarkupLine($"[red bold]Error:[/] Cannot create file [yellow]{Markup.Escape(Path.GetFileName(rawDestPath))}[/]. A folder with the same name already exists at destination.");
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
                AnsiConsole.MarkupLine($"[yellow bold]Notice:[/] The input '[bold]{Markup.Escape(ext)}[/]' is not a [blue].pdf[/] extension.");
                AnsiConsole.MarkupLine($"[yellow bold]Notice:[/] The path will be treated as a [yellow]directory[/], and the PDF will be generated inside it.");
            }

            // 边界检查：检查该路径是否已经是一个“文件”了
            if (File.Exists(rawDestPath))
            {
                AnsiConsole.MarkupLine($"[red bold]Error:[/] Destination path [underline]{Markup.Escape(rawDestPath)}[/] exists and is a file. Please specify a directory or a new .pdf filename.");
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
            AnsiConsole.MarkupLine($"[red]Error scanning files:[/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }

        int totalFiles = rootNode.TotalFileCount();

        if (totalFiles == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No images found in source directory.[/]");
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
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                    new ElapsedTimeColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    // --- 阶段 1: 预处理图片 ---
                    var prepTask = ctx.AddTask("[green]Processing images...[/]");

                    // 1. 准备数据源
                    var allFileLists = NodeHelper
                        .GetAllFileLists(rootNode)
                        .ToList();
                    var workItems = allFileLists
                        .SelectMany(list => list.Select(
                            (file, index) => new ImageJob(list, index, file)))
                        .ToList();

                    prepTask.MaxValue = workItems.Count;

                    // 2. 配置并行通道 (Channels)
                    // jobChannel: 限制容量，防止生产速度远超消费速度导致内存暴涨
                    var jobChannel = Channel.CreateBounded<ImageJob>(new BoundedChannelOptions(500)
                    {
                        SingleWriter = true, // 只有一个生产者循环
                        SingleReader = false // 多个 Worker 抢占读取
                    });

                    // resultChannel: 无界，因为处理结果的处理速度（UI更新/列表赋值）通常很快
                    var resultChannel = Channel.CreateUnbounded<ImageResult>(new UnboundedChannelOptions
                    {
                        SingleWriter = false, // 多个 Worker 和生产者都会写入
                        SingleReader = true   // 只有一个协调者在读取
                    });

                    // 3. 计算 Worker 数量 (保留 1 个核心给 IO/UI/系统)
                    int workerCount = Math.Max(1, Environment.ProcessorCount - 1);

                    // 4-A. 启动协调者 (Coordinator) - 负责 UI 更新和结果汇总
                    var coordinatorTask = Task.Run(async () =>
                    {
                        // 持续读取直到结果通道关闭
                        await foreach (var result in resultChannel.Reader.ReadAllAsync())
                        {
                            if (result.Success)
                            {
                                // 只有路径发生变化时（PNG转JPG）才更新引用和记录
                                // 注意：这里是单线程环境，直接操作 List 是安全的，无需锁
                                if (result.NewPath != result.OriginalPath)
                                {
                                    result.ParentList[result.Index] = result.NewPath!;
                                    tempFiles.Add(result.NewPath!);
                                }
                            }
                            else
                            {
                                // 记录错误并将位置标记为 null (稍后清理)
                                errors.Add((Path.GetFileName(result.OriginalPath), result.Exception!));
                                result.ParentList[result.Index] = null!;
                            }

                            // 统一更新 UI 进度
                            prepTask.Increment(1);
                        }
                    });

                    // 4-B. 启动消费者 (Consumers) - 负责 CPU 密集型转换
                    var consumerTasks = new Task[workerCount];
                    for (int i = 0; i < workerCount; i++)
                    {
                        consumerTasks[i] = Task.Run(async () =>
                        {
                            // 持续读取任务直到任务通道关闭
                            await foreach (var job in jobChannel.Reader.ReadAllAsync())
                            {
                                try
                                {
                                    // 执行耗时的转换逻辑
                                    string newPath = _imageService.ConvertPngToTempJpeg(job.FilePath, jpgQuality);

                                    // 发送成功结果
                                    await resultChannel.Writer.WriteAsync(
                                        new ImageResult(job.ParentList, job.Index, job.FilePath, newPath, true, null));
                                }
                                catch (Exception ex)
                                {
                                    // 发送失败结果
                                    await resultChannel.Writer.WriteAsync(
                                        new ImageResult(job.ParentList, job.Index, job.FilePath, null, false, ex));
                                }
                            }
                        });
                    }

                    // 4-C. 生产者 (Producer) - 主流程负责分发
                    foreach (var item in workItems)
                    {
                        string ext = Path.GetExtension(item.FilePath);

                        // 判断逻辑：是否应当执行转换方法？
                        // 条件：是 PNG 且没有开启 Raw 模式
                        bool needsConversion = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && !settings.Raw;

                        if (needsConversion)
                        {
                            // 需要 CPU 处理：写入任务通道，等待 Worker 处理
                            // 如果通道已满，这里会异步等待，形成自然背压
                            await jobChannel.Writer.WriteAsync(item);
                        }
                        else
                        {
                            // 不需要处理：直接生成结果写入结果通道，绕过 Worker
                            // 这极大减少了线程调度的开销
                            await resultChannel.Writer.WriteAsync(
                                new ImageResult(
                                    item.ParentList, item.Index, item.FilePath, item.FilePath, true, null));
                        }
                    }

                    // 5. 关闭流程
                    // a. 告知所有 Worker：不会有新任务了
                    jobChannel.Writer.Complete();

                    // b. 等待所有 Worker 完成手头的任务
                    await Task.WhenAll(consumerTasks);

                    // c. 告知协调者：不会有新结果了
                    resultChannel.Writer.Complete();

                    // d. 等待协调者处理完剩余的 UI 更新和数据写入
                    await coordinatorTask;

                    // 6. 数据清理
                    // 统一清理处理失败的项 (null)
                    foreach (var fileList in allFileLists)
                        _ = fileList.RemoveAll(f => f == null);

                    prepTask.Value = prepTask.MaxValue;
                    prepTask.StopTask();

                    // 重新计算有效文件数
                    int validFileCount = rootNode.TotalFileCount();
                    if (validFileCount == 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow]No valid files found for PDF generation.[/]");
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
            AnsiConsole.MarkupLine($"[green]Successfully created:[/] {finalPdfLink}");
            return 0;
        }
        else
        {
            // 如果生成了部分文件，提示位置
            if (File.Exists(finalPdfPath))
                AnsiConsole.MarkupLine($"[yellow]PDF created with warnings at:[/] {finalPdfLink}");

            AnsiConsole.MarkupLine($"[yellow]Completed with {errors.Count} errors[/].");
            if (hasCriticalFailure)
                AnsiConsole.MarkupLine("[red bold]Including CRITICAL ERROR.[/]");

            AnsiConsole.Write(new Rule("[red]Failures[/]").LeftJustified());

            foreach (var (ctxName, exception) in errors)
            {
                AnsiConsole.MarkupLine($"[gray bold]Item:[/] [underline]{Markup.Escape(ctxName)}[/]");
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                AnsiConsole.WriteLine();
            }
            return 1;
        }
    }
}
