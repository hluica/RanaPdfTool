using System.Collections.Concurrent;

using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class SplitCommand(IPdfService pdfService) : AsyncCommand<SplitSettings>
{
    private readonly IPdfService _pdfService = pdfService;

    public override async Task<int> ExecuteAsync(CommandContext context, SplitSettings settings, CancellationToken cancellationToken)
    {
        var inputFile = PathHelper.ResolveAbsolutePath(settings.FilePath);

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Invalid PDF file - [/]{Markup.Escape(inputFile)}");
            return 1;
        }

        // 确定输出基础目录
        string baseOutputDir;
        if (!string.IsNullOrEmpty(settings.DestDir))
        {
            baseOutputDir = PathHelper.ResolveAbsolutePath(settings.DestDir);
        }
        else
        {
            baseOutputDir = Path.GetDirectoryName(inputFile)!;
        }

        // 处理子文件夹逻辑
        string finalOutputDir = baseOutputDir;
        if (settings.CreateSubFolder)
        {
            string subFolderName = Path.GetFileNameWithoutExtension(inputFile);
            finalOutputDir = Path.Combine(baseOutputDir, subFolderName);
        }

        Directory.CreateDirectory(finalOutputDir);

        var errors = new ConcurrentBag<(string context, Exception exception)>();

        try
        {
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
                    var task = ctx.AddTask("[green]Scanning pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ExtractImages(
                            inputFile,
                            finalOutputDir,
                            settings.Raw,
                            onProgress: (p) => task.Value = p,
                            onPageError: (pageNum, ex) => errors.Add(($"Page {pageNum}", ex))
                        ));
                });
        }
        catch (Exception ex)
        {
            // 这里的 catch 针对的是诸如文件无法打开等致命错误
            AnsiConsole.MarkupLine($"[red][bold]Fatal error accessing file:[/] [underline]{Markup.Escape(inputFile)}[/][/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }

        if (errors.IsEmpty)
        {
            AnsiConsole.MarkupLine($"[green]Images extracted to:[/] [underline]{Markup.Escape(finalOutputDir)}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Extraction completed with {errors.Count} errors[/].");
            AnsiConsole.Write(new Rule("[red]Extraction Failures[/]").LeftJustified());
            foreach (var (ctxStr, exception) in errors)
            {
                AnsiConsole.MarkupLine($"[gray bold]Context:[/] {Markup.Escape(ctxStr)}");
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
                AnsiConsole.WriteLine();
            }
            return 1;
        }
    }
}
