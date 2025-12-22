using System.ComponentModel;

using Spectre.Console.Cli;

namespace RanaPdfTool.Settings;

public class ModifySettings : CommandSettings
{
    [CommandOption("-f|--file <FILE>")]
    [Description("Path to the PDF file to modify.")]
    public required string FilePath { get; set; }
}
