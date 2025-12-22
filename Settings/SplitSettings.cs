using System.ComponentModel;

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

    [CommandOption("--raw")]
    [Description("If set, keeps original image formats. Otherwise converts to JPEG.")]
    public bool Raw { get; set; }
}
