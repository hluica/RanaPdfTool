using Spectre.Console;
using Spectre.Console.Rendering;

namespace RanaPdfTool.Utils;

/// <summary>
/// Writing ANSI escape sequences to stderr.
/// Derived from Spectre.Console.AnsiConsole.
/// </summary>
public static class StdErr
{
    public static IAnsiConsole AnsiConsoleInstance { get; }
        = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error)
        });

    public static void Markup(string value)
        => AnsiConsoleInstance.Markup(value);

    public static void MarkupLine(string value)
        => AnsiConsoleInstance.MarkupLine(value);
    public static void Write(IRenderable renderable)
        => AnsiConsoleInstance.Write(renderable);

    public static void WriteLine()
        => AnsiConsoleInstance.WriteLine();

    public static void WriteException(Exception exception, ExceptionFormats format = ExceptionFormats.Default)
        => AnsiConsoleInstance.WriteException(exception, format);
}
