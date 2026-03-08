using Spectre.Console;

namespace RanaPdfTool.Utils;

public static class AnsiConsoleExtension
{
    extension(AnsiConsole)
    {
        /// <summary>
        /// Set the output stream of the AnsiConsole to stderr.
        /// </summary>
        public static IAnsiConsole Error
            => AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(Console.Error)
            });
    }
}
