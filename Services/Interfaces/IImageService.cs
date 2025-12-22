namespace RanaPdfTool.Services.Interfaces;

public interface IImageService
{
    string ConvertPngToTempJpeg(string inputPath);
    void SaveBytesAsJpeg(byte[] imageBytes, string outputPath);
    string SaveWithDetectedFormat(byte[] imageBytes, string outputBaseName);
}
