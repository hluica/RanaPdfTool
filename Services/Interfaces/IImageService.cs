namespace RanaPdfTool.Services.Interfaces;

public interface IImageService
{
    void ConvertPngToTempJpegStream(Stream inputStream, Stream outputStream, int quality);
    void SaveBytesAsJpeg(byte[] imageBytes, string outputPath, int quality);
    string SaveWithDetectedFormat(byte[] imageBytes, string outputBaseName);
}
