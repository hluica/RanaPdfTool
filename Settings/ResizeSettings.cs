using System.ComponentModel;

using Spectre.Console.Cli;

namespace RanaPdfTool.Settings;

public class ResizeSettings : CommandSettings
{
    [CommandOption("-f|--file <FILE>")]
    [Description("Path to the PDF file to modify.")]
    public required string FilePath { get; set; }

    [CommandOption("-s|--suffix <SUFFIX>")]
    [Description("The suffix for the new PDF file (ignored if --overwrite is present).")]
    [DefaultValue("resized")]
    public string Suffix { get; set; } = "resized";

    [CommandOption("-o|--overwrite")]
    [Description("Overwrite the original PDF file instead of creating a new one.")]
    public bool Overwrite { get; set; }
}
