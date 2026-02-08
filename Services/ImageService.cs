using RanaPdfTool.Services.Interfaces;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace RanaPdfTool.Services;

public class ImageService : IImageService
{
    public void ConvertPngToTempJpegStream(Stream inputStream, Stream outputStream, int quality)
    {
        // 重置流位置，确保从头读取
        if (inputStream.CanSeek)
            inputStream.Position = 0;

        using var image = Image.Load(inputStream);

        var encoder = new JpegEncoder
        {
            Quality = quality
        };

        image.Save(outputStream, encoder);
    }

    public void SaveBytesAsJpeg(byte[] imageBytes, string outputPath, int quality)
    {
        using var image = Image.Load(imageBytes);
        var encoder = new JpegEncoder
        {
            Quality = quality
        };
        image.Save(outputPath, encoder);
    }

    public string SaveWithDetectedFormat(byte[] imageBytes, string outputBaseName)
    {
        string extension = "dat"; // 默认兜底格式

        try
        {
            // 尝试嗅探格式
            var format = Image.DetectFormat(imageBytes);
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
