using System.Collections.Concurrent;

using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class ModifyCommand(IPdfService pdfService) : AsyncCommand<ModifySettings>
{
    private readonly IPdfService _pdfService = pdfService;

    public override async Task<int> ExecuteAsync(CommandContext context, ModifySettings settings, CancellationToken cancellationToken)
    {
        string inputFile = PathHelper.ResolveAbsolutePath(settings.FilePath);

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Invalid PDF file - [/]{Markup.Escape(inputFile)}");
            return 1;
        }

        string dir = Path.GetDirectoryName(inputFile)!;
        string name = Path.GetFileNameWithoutExtension(inputFile);
        string outputFile = Path.Combine(dir, $"{name}_modified.pdf");

        var errors = new ConcurrentBag<(string context, Exception exception)>();
        bool hasCriticalFailure = false;

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
                    var task = ctx.AddTask("[green]Resizing pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ResizePdfPages(
                            inputFile,
                            outputFile,
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
            AnsiConsole.MarkupLine($"[green]Modified file saved to:[/] [underline]{Markup.Escape(outputFile)}[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Process completed with {errors.Count} errors[/].");
            if (hasCriticalFailure)
                AnsiConsole.MarkupLine("[red bold]Including CRITICAL ERROR.[/]");
            AnsiConsole.Write(new Rule("[red]Page Failures[/]").LeftJustified());
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
