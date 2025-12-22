using RanaPdfTool.Services.Interfaces;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace RanaPdfTool.Services;

public class ImageService : IImageService
{
    public string ConvertPngToTempJpeg(string inputPath)
    {
        using var image = Image.Load(inputPath);

        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");

        var encoder = new JpegEncoder
        {
            Quality = 95
        };

        image.Save(tempFile, encoder);
        return tempFile;
    }

    public void SaveBytesAsJpeg(byte[] imageBytes, string outputPath)
    {
        using var image = Image.Load(imageBytes);
        var encoder = new JpegEncoder
        {
            Quality = 95
        };
        image.Save(outputPath, encoder);
    }

    public string SaveWithDetectedFormat(byte[] imageBytes, string outputBaseName)
    {
        string extension = "dat"; // 默认兜底格式

        try
        {
            // 尝试嗅探格式
            IImageFormat? format = Image.DetectFormat(imageBytes);
            if (format != null)
            {
                // 获取最常见的扩展名 (如 png, bmp, gif)
                extension = format.FileExtensions.FirstOrDefault() ?? "dat";
            }
        }
        catch
        {
            throw; // will be re-catch in PdfService.ExtractImages()
        }

        string fullPath = $"{outputBaseName}.{extension}";
        File.WriteAllBytes(fullPath, imageBytes);

        return fullPath;
    }
}
