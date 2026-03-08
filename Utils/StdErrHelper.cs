using Spectre.Console;
using Spectre.Console.Rendering;

namespace RanaPdfTool.Utils;

/// <summary>
/// Writing ANSI escape sequences to stderr.
/// Derived from Spectre.Console.AnsiConsole.
/// </summary>
public static class StdErr
{
    public static IAnsiConsole Console { get; }
        = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(System.Console.Error)
        });
}
