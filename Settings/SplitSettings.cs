using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Settings;

public class SplitSettings : CommandSettings
{
    [CommandOption("-f|--file <FILE>")]
    [Description("Path to the source PDF file.")]
    public required string FilePath { get; set; }

    [CommandOption("-d|--destination <PATH>")]
    [Description("Optional output directory.")]
    public string? DestDir { get; set; }

    [CommandOption("--subfolder")]
    [Description("Create a subfolder named after the file.")]
    public bool CreateSubFolder { get; set; }

    [CommandOption("-q|--quality <NUMBER>")]
    [Description("JPEG quality level (1-100). If not set, the default is 90. Cannot be used with '--raw'.")]
    [DefaultValue(90)]
    public int? Quality { get; set; }

    [CommandOption("--raw")]
    [Description("If set, keeps original image formats. Otherwise converts to JPEG. Cannot be used with '--quality'.")]
    public bool Raw { get; set; }

    public override ValidationResult Validate()
    {
        if (Raw && Quality.HasValue)
        {
            return ValidationResult.Error("The '--quality' option cannot be used with '--raw'.");
        }

        if (Quality.HasValue && (Quality.Value < 1 || Quality.Value > 100))
        {
            return ValidationResult.Error("Quality must be between 1 and 100.");
        }

        return ValidationResult.Success();
    }
}
