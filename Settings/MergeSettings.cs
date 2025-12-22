using Spectre.Console.Cli;
using System.ComponentModel;

namespace RanaPdfTool.Settings;

public class MergeSettings : CommandSettings
{
    [CommandOption("-s|--source <PATH>")]
    [Description("Source folder containing images.")]
    public required string SourceDir { get; set; }

    [CommandOption("-d|--destination <PATH>")]
    [Description("Output file path OR directory. If directory, filename defaults to source folder name.")]
    public required string DestDir { get; set; }

    [CommandOption("--raw")]
    [Description("If set, PNGs will not be converted to JPEG.")]
    public bool Raw { get; set; }

    [CommandOption("-r|--resize")]
    [Description("If set, resizes pages to fixed width (A4 width) without altering image quality.")]
    public bool Resize { get; set; }
}
