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
        var sourceDir = PathHelper.ResolveAbsolutePath(settings.SourceDir);
        var destInput = PathHelper.ResolveAbsolutePath(settings.DestDir);

        if (!Directory.Exists(sourceDir))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Source directory not found: [underline]{Markup.Escape(sourceDir)}[/][/]");
            return 1;
        }

        string finalPdfPath = string.Empty;

        // 判断输入是文件夹还是文件。使用 Path.HasExtension 作为主要判断依据。
        if (Path.HasExtension(destInput))
        {
            // 情况 A: 用户指定了具体的文件名 (e.g. ./Output/mybook.pdf)
            // 扩展名检查
            string ext = Path.GetExtension(destInput);
            if (!ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red bold]Error:[/] The destination file must have a [bold].pdf[/] extension. You provided: [yellow]{ext}[/]");
                return 1;
            }
            // 确保文件目录存在
            string dir = Path.GetDirectoryName(destInput)!;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            finalPdfPath = destInput;
        }
        else
        {
            // 情况 B: 用户仅指定了目录 (e.g. ./Output)
            if (!Directory.Exists(destInput))
            {
                Directory.CreateDirectory(destInput);
            }

            // 获取源文件夹的名称
            // 如果 sourceDir 是根目录 (e.g. C:\), Name 属性可能为空，需处理
            var dirInfo = new DirectoryInfo(sourceDir);
            string sourceFolderName = string.IsNullOrWhiteSpace(dirInfo.Name) ? "output" : dirInfo.Name;

            finalPdfPath = Path.Combine(destInput, $"{sourceFolderName}.pdf");
        }

        // 自动重命名
        finalPdfPath = PathHelper.GetUniqueFilePath(finalPdfPath);

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
                AnsiConsole.Status().Start("Cleaning up...", _ =>
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
