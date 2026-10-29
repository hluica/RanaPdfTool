using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

namespace RanaPdfTool.Settings;

public class ExtractSettings : CommandSettings
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

    // 不要在此处使用 [DefaultValue(90)]：Spectre.Console.Cli 会把默认值写入属性，
    // 导致未显式指定 --quality 时也会与 --raw 冲突。默认值由命令自行处理。
    [CommandOption("-q|--quality <NUMBER>")]
    [Description("JPEG quality level (1-100). If not set, the default is 90. Cannot be used with '--raw'.")]
    public int? Quality { get; set; }

    [CommandOption("--raw")]
    [Description("If set, keeps original image formats. Otherwise converts to JPEG. Cannot be used with '--quality'.")]
    public bool Raw { get; set; }

    public override ValidationResult Validate()
        => (Raw, Quality) switch
        {
            (true, not null) => ValidationResult.Error("The '--quality' option cannot be used with '--raw'."),
            (_, < 1 or > 100) => ValidationResult.Error("Quality must be between 1 and 100."),
            _ => ValidationResult.Success()
        };
}
