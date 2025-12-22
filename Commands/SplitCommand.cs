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
                    var task = ctx.AddTask("[green]Scanning pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ExtractImages(
                            inputFile,
                            finalOutputDir,
                            settings.Raw,
                            (p) => task.Value = p
                        ));
                });

            AnsiConsole.MarkupLine($"[green]Images extracted to:[/] [underline]{Markup.Escape(finalOutputDir)}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red][bold]Failed to extract images from file:[/] [underline]{Markup.Escape(inputFile)}[/][/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }
}
