namespace RanaPdfTool.Services.Interfaces;

public interface IImageService
{
    string ConvertPngToTempJpeg(string inputPath, int quality);
    void SaveBytesAsJpeg(byte[] imageBytes, string outputPath, int quality);
    string SaveWithDetectedFormat(byte[] imageBytes, string outputBaseName);
}
