using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Settings;

public class MergeSettings : CommandSettings
{
    [CommandOption("-s|--source <PATH>")]
    [Description("Source folder containing images.")]
    public required string SourceDir { get; set; }

    [CommandOption("-d|--destination <PATH>")]
    [Description("Output file path OR directory. If directory, filename defaults to source folder name.")]
    public required string DestDir { get; set; }

    [CommandOption("-q|--quality <NUMBER>")]
    [Description("JPEG quality level (1-100). If not set, the default is 90. Cannot be used with '--raw'.")]
    [DefaultValue(90)]
    public int? Quality { get; set; }

    [CommandOption("--raw")]
    [Description("If set, PNGs will not be converted to JPEG. Cannot be used with '--quality'.")]
    public bool Raw { get; set; }

    [CommandOption("-r|--resize")]
    [Description("If set, resizes pages to fixed width (A4 width) without altering image quality.")]
    public bool Resize { get; set; }

    public override ValidationResult Validate()
    {
        // 互斥检查：如果 Raw 为 true 且 Quality 有值，则报错
        if (Raw && Quality.HasValue)
        {
            return ValidationResult.Error("The '--quality' option cannot be used with '--raw'.");
        }

        // 范围检查
        if (Quality.HasValue && (Quality.Value < 1 || Quality.Value > 100))
        {
            return ValidationResult.Error("Quality must be between 1 and 100.");
        }

        return ValidationResult.Success();
    }
}
