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
        var inputFile = PathHelper.ResolveAbsolutePath(settings.FilePath);

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Invalid PDF file - [/]{Markup.Escape(inputFile)}");
            return 1;
        }

        var dir = Path.GetDirectoryName(inputFile)!;
        var name = Path.GetFileNameWithoutExtension(inputFile);
        var outputFile = Path.Combine(dir, $"{name}_modified.pdf");

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns([
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                ])
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Resizing pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ResizePdfPages(
                            inputFile,
                            outputFile,
                            (p) => task.Value = p
                        ));
                });

            AnsiConsole.MarkupLine($"[green]Modified file saved to:[/] [underline]{Markup.Escape(outputFile)}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }
}
