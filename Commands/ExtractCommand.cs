using System.Collections.Concurrent;

using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class ExtractCommand(IPdfService pdfService) : AsyncCommand<ExtractSettings>
{
    private readonly IPdfService _pdfService = pdfService;

    private static readonly Color _processingAccentColor = ColorHelper.GetWindowsAccentColor(Color.Yellow);
    private static readonly Color _finishedAccentColor = ColorHelper.GetWindowsAccentColor(Color.Green);

    public override async Task<int> ExecuteAsync(CommandContext context, ExtractSettings settings, CancellationToken cancellationToken)
    {
        int jpgQuality = settings.Quality ?? 90;

        var (fileOk, inputFile) = CliGuard.TryRun<string, ArgumentException>(
            () => PathHelper.ResolveAbsolutePath(settings.FilePath),
            $"Invalid file path: {settings.FilePath}");

        if (!fileOk || string.IsNullOrEmpty(inputFile))
            return 1;

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red][[ERROR]][/] Invalid PDF file: [red underline]{Markup.Escape(inputFile)}[/]");
            return 1;
        }

        // 确定输出基础目录
        string baseOutputDir = !string.IsNullOrEmpty(settings.DestDir)
            ? PathHelper.ResolveAbsolutePath(settings.DestDir)
            : Path.GetDirectoryName(inputFile)!;

        // 处理子文件夹逻辑
        string finalOutputDir = baseOutputDir;
        if (settings.CreateSubFolder)
        {
            string subFolderName = Path.GetFileNameWithoutExtension(inputFile);
            finalOutputDir = Path.Combine(baseOutputDir, subFolderName);
        }

        _ = Directory.CreateDirectory(finalOutputDir);
        string finalOutputLink = MarkupHelper.FileLinkMarkup(finalOutputDir);

        var errors = new ConcurrentBag<(string context, Exception exception)>();
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
                        CompletedStyle = new Style(_processingAccentColor),
                        FinishedStyle = new Style(_finishedAccentColor)
                    },
                    new PercentageColumn
                    {
                        CompletedStyle = new Style(_finishedAccentColor),
                    },
                    new SpinnerColumn
                    {
                        Style = new Style(_processingAccentColor),
                    },
                    new ElapsedTimeColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Scanning pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ExtractImages(
                            inputFile,
                            finalOutputDir,
                            jpgQuality,
                            settings.Raw,
                            onProgress: (p) => task.Value = p,
                            onPageError: (pageNum, ex) => errors.Add(($"Page {pageNum}", ex))
                        ));
                });
        }
        catch (Exception ex)
        {
            errors.Add(("CRITICAL EXECUTION ERROR", ex));
            hasCriticalFailure = true;
        }

        if (errors.IsEmpty)
        {
            AnsiConsole.MarkupLine($"[green][[SUCCESS]][/] Images extracted to: {finalOutputLink}");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red][[ERROR]][/] Extraction completed with [red bold]{errors.Count}[/] errors.");
            if (hasCriticalFailure)
                AnsiConsole.MarkupLine("[red][[ERROR]][/] Including [bold]CRITICAL ERROR[/].");
            AnsiConsole.Write(new Rule("[red]Extract Failures[/]").LeftJustified());
            foreach (var (ctxStr, exception) in errors)
            {
                AnsiConsole.MarkupLine($"[gray bold]Context:[/] [underline]{Markup.Escape(ctxStr)}[/]");
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                AnsiConsole.WriteLine();
            }
            return 1;
        }
    }
}
