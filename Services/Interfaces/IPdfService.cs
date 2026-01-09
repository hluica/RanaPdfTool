using RanaPdfTool.Models;

namespace RanaPdfTool.Services.Interfaces;

public interface IPdfService
{
    void MergeImagesToPdf(
        DirectoryNode rootNode,
        string outputPdfPath,
        bool doResize,
        int totalFilesExpectation,
        Action<double>? onProgress = null,
        Action<string, Exception>? onItemError = null);
    void ResizePdfPages(
        string inputPdfPath,
        string outputPdfPath,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null);
    void ExtractImages(
        string inputPdfPath,
        string outputDirectory,
        int quality,
        bool rawMode,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null);
}
