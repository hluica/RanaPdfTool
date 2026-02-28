using System.Collections.Concurrent;

using RanaPdfTool.Services.Interfaces;
using RanaPdfTool.Settings;
using RanaPdfTool.Utils;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Commands;

public class ResizeCommand(IPdfService pdfService) : AsyncCommand<ResizeSettings>
{
    private readonly IPdfService _pdfService = pdfService;

    private static readonly Color _processingAccentColor = ColorHelper.GetWindowsAccentColor(Color.Yellow);
    private static readonly Color _finishedAccentColor = ColorHelper.GetWindowsAccentColor(Color.Green);

    public override async Task<int> ExecuteAsync(CommandContext context, ResizeSettings settings, CancellationToken cancellationToken)
    {
        string inputFile = PathHelper.ResolveAbsolutePath(settings.FilePath);

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red][bold]Error:[/] Invalid PDF file - [/]{Markup.Escape(inputFile)}");
            return 1;
        }

        string dir = Path.GetDirectoryName(inputFile)!;
        string name = Path.GetFileNameWithoutExtension(inputFile);
        string workingOutputFile = Path.Combine(dir, $"{name}_{settings.Suffix}.pdf");

        var errors = new ConcurrentBag<(string context, Exception exception)>();
        bool hasCriticalFailure = false;

        // 生成临时文件
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
                    var task = ctx.AddTask("[green]Resizing pages...[/]");

                    await Task.Run(() =>
                        _pdfService.ResizePdfPages(
                            inputFile,
                            workingOutputFile,
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

        // 显示生成中的错误
        if (!errors.IsEmpty)
        {
            AnsiConsole.MarkupLine($"[yellow]Page processing completed with {errors.Count} errors[/].");
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

        // 文件路由和保存
        string finalPath = workingOutputFile;
        if (settings.Overwrite)
        {
            try
            {
                // 保存且覆盖原文件：使用原子移动。
                File.Move(workingOutputFile, inputFile, overwrite: true);
                finalPath = inputFile;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error overwriting original file:[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                return 1;
            }
        }

        // 保存但不覆盖：直接使用临时文件。
        string outputLink = MarkupHelper.FileLinkMarkup(finalPath);
        string actionText = settings.Overwrite ? "Overwritten" : "Saved";
        AnsiConsole.MarkupLine($"[green]Modified file {actionText} to:[/] {outputLink}");
        return 0;
    }
}
