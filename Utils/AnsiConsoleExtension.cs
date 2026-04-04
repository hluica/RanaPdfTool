using Spectre.Console;

namespace RanaPdfTool.Utils;

public static class AnsiConsoleExtension
{
    /// <summary>
    /// Set the output stream of the AnsiConsole to stderr.
    /// </summary>
    private static readonly Lazy<IAnsiConsole> _ErrorConsole = new(
        () => AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error),
        }));

    extension(AnsiConsole)
    {
        /// <summary>
        /// Get the AnsiConsole that writes to stderr.
        /// </summary>
        public static IAnsiConsole Error
            => _ErrorConsole.Value;
    }
}
