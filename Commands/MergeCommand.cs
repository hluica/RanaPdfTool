using System.Collections.Concurrent;

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
        var errors = new ConcurrentBag<(string context, Exception exception)>();
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
                    new RemainingTimeColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    // --- 阶段 1: 预处理图片 ---
                    var prepTask = ctx.AddTask("[green]Processing images...[/]", maxValue: 100);

                    // 1. 准备打平的任务列表 (串行)
                    var allFileLists = NodeHelper
                        .GetAllFileLists(rootNode)
                        .ToList();
                    var workItems = allFileLists
                        .SelectMany(list => list.Select(
                            (file, index) => new
                            {
                                List = list,
                                Index = index,
                                FilePath = file
                            }))
                        .ToList();

                    int totalFiles = workItems.Count;
                    int processedCount = 0;

                    // 用于存储并行结果的容器 (线程安全)
                    var processingResults = new ConcurrentBag<ImageProcessingResult>();

                    // 2. 并行处理阶段
                    await Task.Run(() =>
                    {
                        var parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount
                        };

                        _ = Parallel.ForEach(workItems, parallelOptions, item =>
                        {
                            string? finalPath = null;
                            Exception? capturedException = null;
                            bool isSuccess = false;

                            try
                            {
                                // 1. 核心业务逻辑：仅做数据处理，不操作外部集合
                                string ext = Path.GetExtension(item.FilePath);

                                finalPath = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) && !settings.Raw
                                    ? _imageService.ConvertPngToTempJpeg(item.FilePath, jpgQuality)
                                    : item.FilePath;

                                isSuccess = true;
                            }
                            catch (Exception ex)
                            {
                                // 2. 异常捕获：仅记录状态
                                capturedException = ex;
                                isSuccess = false;
                                finalPath = null;
                            }
                            finally
                            {
                                // 3. 单点提交区 (Single Point of Submission)
                                processingResults.Add(
                                    new ImageProcessingResult(
                                        item.List,
                                        item.Index,
                                        item.FilePath,
                                        finalPath,
                                        isSuccess,
                                        capturedException
                                    ));

                                // 4. 进度更新
                                int current = Interlocked.Increment(ref processedCount);
                                prepTask.Value = (double)current / totalFiles * 100;
                            }
                        });
                    });

                    // 3. 串行更新阶段 (更新外部数据结构)
                    // 在这个阶段，我们处理所有收集到的结果，并更新 rootNode 的引用
                    foreach (var result in processingResults)
                    {
                        if (result.IsSuccess)
                        {
                            // 如果路径发生了变化（PNG -> JPG），则更新列表并记录临时文件
                            if (result.NewPath != result.OriginalPath)
                            {
                                result.ParentList[result.Index] = result.NewPath!;
                                tempFiles.Add(result.NewPath!);
                            }
                        }
                        else
                        {
                            // 处理失败：记录错误，并将该位置标记为待删除
                            errors.Add((Path.GetFileName(result.OriginalPath), result.Exception!));
                            result.ParentList[result.Index] = null!; // 暂时占位
                        }
                    }

                    // 统一清理处理失败的项
                    foreach (var fileList in allFileLists)
                        _ = fileList.RemoveAll(f => f == null);

                    prepTask.StopTask();

                    // 重新计算有效文件数 (因为可能有失败被移除的)
                    int validFileCount = rootNode.TotalFileCount();
                    if (validFileCount == 0)
                        return;

                    // --- 阶段 2: 生成 PDF ---
                    var mergeTask = ctx.AddTask("[green]Generating PDF...[/]", maxValue: 100);

                    await Task.Run(() =>
                        _pdfService.MergeImagesToPdf(
                            rootNode, // 传入树根节点
                            finalPdfPath,
                            settings.Resize,
                            totalFilesExpectation: validFileCount, // 传入总数用于计算进度
                            onProgress: (p) => mergeTask.Value = p,
                            onItemError: (fileName, ex) => errors.Add((fileName, ex))
                        ));

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
        if (errors.IsEmpty)
        {
            AnsiConsole.MarkupLine($"[green]Successfully created:[/] [underline]{Markup.Escape(finalPdfPath)}[/]");
            return 0;
        }
        else
        {
            // 如果生成了部分文件，提示位置
            if (File.Exists(finalPdfPath))
                AnsiConsole.MarkupLine($"[yellow]PDF created with warnings at:[/] [underline]{Markup.Escape(finalPdfPath)}[/]");

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
