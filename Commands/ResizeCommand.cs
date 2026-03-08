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

    public override async Task<int> ExecuteAsync(CommandContext context, ResizeSettings settings, CancellationToken cancellationToken)
    {
        var (fileOk, inputFile) = CliGuard.TryRun<string, ArgumentException>(
            () => PathHelper.ResolveAbsolutePath(settings.FilePath),
            $"Invalid file path: {settings.FilePath}");

        if (!fileOk || string.IsNullOrEmpty(inputFile))
            return 1;

        if (!File.Exists(inputFile) || !Path.GetExtension(inputFile).Equals(".pdf", StringComparison.CurrentCultureIgnoreCase))
        {
            StdErr.Console.MarkupLine($"[red][[ERROR]][/] Invalid PDF file: [red underline]{Markup.Escape(inputFile)}[/]");
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
                        CompletedStyle = new Style(ColorHelper.ProcessingAccentColor),
                        FinishedStyle = new Style(ColorHelper.FinishedAccentColor)
                    },
                    new PercentageColumn
                    {
                        CompletedStyle = new Style(ColorHelper.FinishedAccentColor),
                    },
                    new SpinnerColumn
                    {
                        Style = new Style(ColorHelper.ProcessingAccentColor),
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
            StdErr.Console.MarkupLine($"[red][[ERROR]][/] Page processing completed with [red bold]{errors.Count}[/] errors.");
            if (hasCriticalFailure)
                StdErr.Console.MarkupLine("[red][[ERROR]][/] Including [bold]CRITICAL ERROR[/].");
            StdErr.Console.Write(new Rule("[red]Page Failures[/]").LeftJustified());
            foreach (var (ctxStr, exception) in errors)
            {
                StdErr.Console.MarkupLine($"[gray bold]Context:[/] [underline]{Markup.Escape(ctxStr)}[/]");
                StdErr.Console.WriteException(exception, ExceptionFormats.ShortenEverything);
                StdErr.Console.WriteLine();
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
                StdErr.Console.MarkupLine($"[red][[ERROR]][/] Error overwriting original file:");
                StdErr.Console.WriteException(ex, ExceptionFormats.ShortenEverything);
                return 1;
            }
        }

        // 保存但不覆盖：直接使用临时文件。
        string outputLink = MarkupHelper.FileLinkMarkup(finalPath);
        string actionText = settings.Overwrite ? "Overwritten" : "Saved";
        AnsiConsole.MarkupLine($"[green][[SUCCESS]][/] Modified file {actionText} to: [green underline]{outputLink}[/]");
        return 0;
    }
}
