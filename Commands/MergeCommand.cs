using NaturalSort.Extension;

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
                // 使用 CliGuard 捕获创建文件夹时的权限或IO错误
                // 这里使用 Exception 作为泛型，因为 CreateDirectory 可能抛出多种 IO/Auth 异常
                bool createDirOk = CliGuard.TryRun<Exception>(
                    () => Directory.CreateDirectory(parentDir),
                    $"Failed to create directory: {parentDir}. Check permissions.");

                if (!createDirOk) return 1;
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

                if (!createDirOk) return 1;
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

        // 获取所有图片文件，使用自然排序
        var extensions = new[] { ".jpg", ".jpeg", ".png" };
        var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f, naturalComparer) // 按名称排序
            .ToList();

        if (allFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No images found in source directory.[/]");
            return 0;
        }

        var finalPaths = new List<string>();
        var tempFiles = new List<string>();
        bool hasError = false;

        try
        {
            // 启动进度条上下文
            await AnsiConsole.Progress()
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
                    // 阶段 1: 预处理图片
                    var prepTask = ctx.AddTask("[green]Processing images...[/]", maxValue: 100);

                    int totalFiles = allFiles.Count;
                    int processedCount = 0;

                    // 注意：这里我们不能把循环放入 Task.Run，因为我们要操作 finalPaths 和 tempFiles 集合
                    foreach (var file in allFiles)
                    {
                        var ext = Path.GetExtension(file).ToLower();

                        if ((ext == ".png") && !settings.Raw)
                        {
                            var tempJpg = _imageService.ConvertPngToTempJpeg(file);
                            finalPaths.Add(tempJpg);
                            tempFiles.Add(tempJpg);
                        }
                        else
                        {
                            finalPaths.Add(file);
                        }

                        processedCount++;
                        prepTask.Value = (double)processedCount / totalFiles * 100;
                    }

                    // 标记第一阶段完成
                    prepTask.StopTask();

                    // 阶段 2: 生成 PDF
                    var mergeTask = ctx.AddTask("[green]Generating PDF...[/]", maxValue: 100);

                    await Task.Run(() =>
                        _pdfService.MergeImagesToPdf(
                            finalPaths,
                            finalPdfPath,
                            settings.Resize,
                            (p) => mergeTask.Value = p
                        ));

                    // 标记第二阶段完成
                    mergeTask.StopTask();
                });

            AnsiConsole.MarkupLine($"[green]Successfully created:[/] [underline]{Markup.Escape(finalPdfPath)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            hasError = true;
        }
        finally
        {
            // 清理临时文件（通常很快，用 Status 即可，或者如果不耗时直接删）
            if (tempFiles.Count != 0)
            {
                AnsiConsole
                    .Status()
                    .Start("Cleaning up...", _ =>
                    {
                        foreach (var temp in tempFiles)
                        {
                            if (File.Exists(temp))
                                File.Delete(temp);
                        }
                    });
            }
        }

        return hasError ? 1 : 0;
    }
}
