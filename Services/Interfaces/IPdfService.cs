namespace RanaPdfTool.Services.Interfaces;

public interface IPdfService
{
    void MergeImagesToPdf(
        List<string> imagePaths,
        string outputPdfPath,
        bool doResize,
        Action<double>? onProgress = null);
    void ResizePdfPages(
        string inputPdfPath,
        string outputPdfPath,
        Action<double>? onProgress = null);
    void ExtractImages(
        string inputPdfPath,
        string outputDirectory,
        bool rawMode,
        Action<double>? onProgress = null);
}
