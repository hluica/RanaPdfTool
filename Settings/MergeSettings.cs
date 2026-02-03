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
        => (Raw, Quality) switch
        {
            (true, not null) => ValidationResult.Error("The '--quality' option cannot be used with '--raw'."),
            (_, < 1 or > 100) => ValidationResult.Error("Quality must be between 1 and 100."),
            _ => ValidationResult.Success()
        };
}
