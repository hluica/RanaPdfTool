using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;

using RanaPdfTool.Models;
using RanaPdfTool.Services.Interfaces;

namespace RanaPdfTool.Services;

public class PdfService(IImageService imageService) : IPdfService
{
    private readonly IImageService _imageService = imageService;

    // 使用 A4 宽度作为需要固定页面宽度时的目标宽度
    private const float TARGET_PAGE_WIDTH = 595.276f;

    /// <summary>
    /// Calculates the transformation parameters required to scale and reposition a rectangle to a specified target
    /// width, preserving its aspect ratio and normalizing its origin to (0, 0).
    /// </summary>
    /// <remarks>The transformation preserves the aspect ratio of the original rectangle. The resulting
    /// transformation matrix can be used to map coordinates from the original rectangle to the new, normalized
    /// rectangle.</remarks>
    /// <param name="originalBox">The original rectangle to be transformed. Represents the source coordinates and dimensions.</param>
    /// <param name="targetWidth">The desired width, in points, for the transformed rectangle. Must be greater than zero to apply scaling.</param>
    /// <returns>A tuple containing the new rectangle with normalized origin and target width, followed by the transformation
    /// matrix parameters: scaleX, skewY, skewX, scaleY, shiftX, and shiftY. If the original rectangle's width is not
    /// positive, returns the original rectangle and an identity transformation.</returns>
    private static
        (Rectangle newbox,
        double scaleX, double skewY, double skewX,
        double scaleY, double shiftX, double shiftY)
        ComputePageTransform(Rectangle originalBox, float targetWidth)
    {
        double width = originalBox.GetWidth();
        double height = originalBox.GetHeight();
        double llx = originalBox.GetX();
        double lly = originalBox.GetY();

        if (width <= 0)
            return (originalBox, 1, 0, 0, 1, 0, 0);

        // 1. 计算缩放
        double scale = targetWidth / width;
        double targetHeight = height * scale;

        // 2. 生成新边界 (归一化到 0,0)
        var newBox = new Rectangle(0, 0, targetWidth, (float)targetHeight);

        // 3. 计算位移 (将原始起点的偏移量反向抵消，并应用缩放)
        double shiftX = -llx * scale;
        double shiftY = -lly * scale;

        // 返回计算结果
        return (newBox, scale, 0, 0, scale, shiftX, shiftY);
    }

    public void MergeImagesToPdf(
        DirectoryNode rootNode,
        string outputPdfPath,
        bool doResize,
        int totalFilesExpectation,
        Action<double>? onProgress = null,
        Action<string, Exception>? onItemError = null)
    {
        using var writer = new PdfWriter(outputPdfPath);
        using var pdfDoc = new PdfDocument(writer);
        using var doc = new Document(pdfDoc);

        // 移除默认边距
        doc.SetMargins(0, 0, 0, 0);

        // 获取 PDF 的根书签对象
        var rootOutline = pdfDoc.GetOutlines(false);

        // 用于在递归中追踪进度
        int processedCount = 0;

        // 开始递归处理
        _ = ProcessNode(rootNode, pdfDoc, rootOutline, isRoot: true);

        // --- 内部递归函数 ---
        // 返回值：该节点产生的（或其子节点产生的）第一页，用于设置父级书签跳转目标
        PdfPage? ProcessNode(DirectoryNode node, PdfDocument pdf, PdfOutline parentOutline, bool isRoot)
        {
            PdfOutline? currentOutline = null;
            PdfPage? firstPageOfNode = null;

            // 1. 创建书签 (如果不是根节点)
            if (!isRoot)
                currentOutline = parentOutline.AddOutline(node.Name);
            else
                // 根节点直接挂载在文档根上，不创建视觉书签
                currentOutline = parentOutline;

            // 2. 处理当前节点的文件
            foreach (string path in node.Files)
            {
                try
                {
                    var imageData = ImageDataFactory.Create(path);
                    var image = new Image(imageData);

                    float imgWidth = imageData.GetWidth();
                    float imgHeight = imageData.GetHeight();

                    var originalSize = new PageSize(imgWidth, imgHeight);
                    var page = pdf.AddNewPage(originalSize);

                    // 记录该节点的第一页
                    firstPageOfNode ??= page;

                    if (doResize)
                    {
                        var (newBox, a, b, c, d, e, f) = ComputePageTransform(originalSize, TARGET_PAGE_WIDTH);
                        _ = page.SetMediaBox(newBox);
                        _ = page.SetCropBox(newBox);
                        _ = new PdfCanvas(page).ConcatMatrix(a, b, c, d, e, f);
                    }

                    var canvas = new PdfCanvas(page);
                    _ = canvas.AddXObjectFittedIntoRectangle(image.GetXObject(), new Rectangle(0, 0, imgWidth, imgHeight));
                }
                catch (Exception ex)
                {
                    onItemError?.Invoke(System.IO.Path.GetFileName(path), ex);
                }
                finally
                {
                    processedCount++;
                    if (totalFilesExpectation > 0)
                        onProgress?.Invoke((double)processedCount / totalFilesExpectation * 100);
                }
            }

            // 3. 处理子文件夹
            foreach (var childNode in node.Children)
            {
                // 递归调用，将当前书签作为父级
                var childPage = ProcessNode(childNode, pdf, currentOutline, isRoot: false);

                // 如果当前文件夹没有文件，尝试使用子文件夹的第一页作为当前文件夹的跳转目标
                firstPageOfNode ??= childPage;
            }

            // 4. 设置书签跳转目标
            // 只有当我们创建了新书签 (!isRoot) 且找到了目标页面时才设置
            if (!isRoot && currentOutline != null && firstPageOfNode != null)
                currentOutline.AddDestination(PdfExplicitDestination.CreateFit(firstPageOfNode));

            return firstPageOfNode;
        }
    }

    public void ResizePdfPages(
        string inputPdfPath,
        string outputPdfPath,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null)
    {
        using var reader = new PdfReader(inputPdfPath);
        using var writer = new PdfWriter(outputPdfPath);
        using var pdfDoc = new PdfDocument(reader, writer);

        int numberOfPages = pdfDoc.GetNumberOfPages();

        for (int i = 1; i <= numberOfPages; i++)
        {
            try
            {
                var page = pdfDoc.GetPage(i);

                var (newBox, a, b, c, d, e, f) = ComputePageTransform(page.GetMediaBox(), TARGET_PAGE_WIDTH);

                _ = page.SetMediaBox(newBox);
                _ = page.SetCropBox(newBox);
                _ = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdfDoc)
                    .ConcatMatrix(a, b, c, d, e, f);
            }
            catch (Exception ex)
            {
                // 错误回调，包含页码
                onPageError?.Invoke(i, ex);
            }

            // 进度回调
            onProgress?.Invoke((double)i / numberOfPages * 100);
        }
    }

    public void ExtractImages(
        string inputPdfPath,
        string outputDirectory,
        int quality,
        bool rawMode,
        Action<double>? onProgress = null,
        Action<int, Exception>? onPageError = null)
    {
        using var reader = new PdfReader(inputPdfPath);
        using var pdfDoc = new PdfDocument(reader);

        int numberOfPages = pdfDoc.GetNumberOfPages();

        for (int i = 1; i <= numberOfPages; i++)
        {
            try
            {
                var page = pdfDoc.GetPage(i);
                var resources = page.GetResources();
                var xObjects = resources.GetResource(PdfName.XObject);

                if (xObjects != null)
                {
                    int imgIndex = 0;
                    foreach (var key in xObjects.KeySet())
                    {
                        try
                        {
                            var stream = xObjects.GetAsStream(key);
                            if (stream == null || !PdfName.Image.Equals(stream.Get(PdfName.Subtype)))
                                continue;

                            imgIndex++;
                            string fileBaseName = $"page_{i}_img_{imgIndex}";

                            var imageXObject = new PdfImageXObject(stream);
                            byte[] imageBytes = imageXObject.GetImageBytes();
                            if (imageBytes == null || imageBytes.Length == 0)
                                continue;

                            string baseOutputPath = System.IO.Path.Combine(outputDirectory, fileBaseName);

                            if (rawMode)
                            {

                                // 1. 优先询问 iText：根据 PDF 字典里的 Filter，这到底是个什么东西？
                                // IdentifyImageFileExtension 会识别 DCTDecode -> jpg, JPXDecode -> jp2, CCITT -> tif 等
                                string? extension = imageXObject.IdentifyImageFileExtension();

                                if (!string.IsNullOrWhiteSpace(extension))
                                {
                                    // 命中了 PDF 标准格式，直接信任 iText
                                    string fullPath = $"{baseOutputPath}.{extension}";
                                    File.WriteAllBytes(fullPath, imageBytes);
                                }
                                else
                                {
                                    // 2. iText 返回 null (通常是 FlateDecode)。
                                    // 这时我们不知道它是“原生像素流”还是“嵌入的完整图片文件(如PNG)”，交给 ImageService 进行字节嗅探。
                                    _ = _imageService.SaveWithDetectedFormat(imageBytes, baseOutputPath);
                                }
                            }
                            else
                            {
                                // ... (非 Raw 模式的逻辑保持不变)
                                bool isJpeg = false;
                                var filter = stream.Get(PdfName.Filter);
                                if (filter != null)
                                {
                                    if (filter is PdfName name && PdfName.DCTDecode.Equals(name))
                                        isJpeg = true;
                                    else if (filter is PdfArray arr && arr.Contains(PdfName.DCTDecode))
                                        isJpeg = true;
                                }

                                if (isJpeg)
                                    File.WriteAllBytes($"{baseOutputPath}.jpg", imageBytes);
                                else
                                    _imageService.SaveBytesAsJpeg(imageBytes, $"{baseOutputPath}.jpg", quality);
                            }
                        }
                        catch (Exception imgEx)
                        {
                            // 获取资源级别的失败
                            onPageError?.Invoke(i, new Exception($"Image #{imgIndex} (Key: {key}) failed.", imgEx));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 获取资源和页面级别的失败
                onPageError?.Invoke(i, ex);
            }

            // 在外层循环（页）结束时汇报进度
            onProgress?.Invoke((double)i / numberOfPages * 100);
        }
    }
}
