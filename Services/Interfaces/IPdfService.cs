using RanaPdfTool.Models;

namespace RanaPdfTool.Services.Interfaces;

public interface IPdfService
{
    /// <summary>
    /// 执行“图片合并为 PDF”的后台方法。
    /// </summary>
    /// <param name="rootNode">用于从中读取文件的目录树</param>
    /// <param name="outputPdfPath">输出 PDF 文件的路径</param>
    /// <param name="doResize">决定是否调整图片大小</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="onItemError">文件错误回调</param>
    void MergeImagesToPdf(
        DirectoryNode rootNode,
        string outputPdfPath,
        bool doResize,
        Action? onProgress = null,
        Action<string, Exception>? onItemError = null);

    /// <summary>
    /// 执行“重设 PDF 页面大小”的后台方法。
    /// </summary>
    /// <param name="inputPdfPath">输入 PDF 文件的路径</param>
    /// <param name="outputPdfPath">输出 PDF 文件的路径</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="onPageError">页面错误回调</param>
    void ResizePdfPages(
        string inputPdfPath,
        string outputPdfPath,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null);

    /// <summary>
    /// 执行“提取 PDF 中的图片对象”的后台方法。
    /// </summary>
    /// <param name="inputPdfPath">输入 PDF 文件的路径</param>
    /// <param name="outputDirectory">用于保存输出图片的目录</param>
    /// <param name="quality">图片质量</param>
    /// <param name="rawMode">决定是否以原始图片格式存储</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="onPageError">页面错误回调</param>
    void ExtractImages(
        string inputPdfPath,
        string outputDirectory,
        int quality,
        bool rawMode,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null);
}
