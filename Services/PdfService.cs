using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;

using RanaPdfTool.Services.Interfaces;

namespace RanaPdfTool.Services;

public class PdfService(IImageService imageService) : IPdfService
{
    private readonly IImageService _imageService = imageService;

    // 使用 A4 宽度作为需要固定页面宽度时的目标宽度
    private const float TargetPageWidth = 595f;

    // 用于承载返回参数的记录结构体
    private readonly record struct PageTransform(
        Rectangle NewBox,
        double ScaleX, double SkewY, double SkewX,
        double ScaleY, double ShiftX, double ShiftY);

    // 工具函数：返回“缩放单个页面内容和边界到指定宽度”所需几何变换参数
    private static PageTransform ComputePageTransform(Rectangle originalBox, float targetWidth)
    {
        float width = originalBox.GetWidth();
        float height = originalBox.GetHeight();
        float llx = originalBox.GetX();
        float lly = originalBox.GetY();

        if (width <= 0)
            return new PageTransform(originalBox, 1, 0, 0, 1, 0, 0);

        // 1. 计算缩放
        float scale = targetWidth / width;
        float targetHeight = height * scale;

        // 2. 生成新边界 (归一化到 0,0)
        var newBox = new Rectangle(0, 0, targetWidth, targetHeight);

        // 3. 计算位移 (将原始起点的偏移量反向抵消，并应用缩放)
        float shiftX = -llx * scale;
        float shiftY = -lly * scale;

        // 返回计算结果
        return new PageTransform(newBox, scale, 0, 0, scale, shiftX, shiftY);
    }

    public void MergeImagesToPdf(
        List<string> imagePaths,
        string outputPdfPath,
        bool doResize,
        Action<double>? onProgress = null,
        Action<string, Exception>? onItemError = null)
    {
        using var writer = new PdfWriter(outputPdfPath);
        using var pdfDoc = new PdfDocument(writer);
        using var doc = new Document(pdfDoc);

        // 移除默认边距
        doc.SetMargins(0, 0, 0, 0);

        // 配置进度回调
        int totalCount = imagePaths.Count;

        for (int i = 0; i < totalCount; i++)
        {
            var path = imagePaths[i];

            try
            {
                var imageData = ImageDataFactory.Create(path);
                var image = new Image(imageData);

                float imgWidth = imageData.GetWidth();
                float imgHeight = imageData.GetHeight();

                var originalSize = new PageSize(imgWidth, imgHeight);
                var page = pdfDoc.AddNewPage(originalSize);

                if (doResize)
                {
                    var (newBox, a, b, c, d, e, f) = ComputePageTransform(originalSize, TargetPageWidth);
                    page.SetMediaBox(newBox);
                    page.SetCropBox(newBox);
                    new PdfCanvas(page).ConcatMatrix(a, b, c, d, e, f);
                }

                var canvas = new PdfCanvas(page);
                canvas.AddXObjectFittedIntoRectangle(image.GetXObject(), new Rectangle(0, 0, imgWidth, imgHeight));
            }
            catch (Exception ex)
            {
                // 错误回调，包含错误文件信息
                onItemError?.Invoke(System.IO.Path.GetFileName(path), ex);
            }

            // 进度回调
            onProgress?.Invoke((double)(i + 1) / totalCount * 100);
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

                var (newBox, a, b, c, d, e, f) = ComputePageTransform(page.GetMediaBox(), TargetPageWidth);

                page.SetMediaBox(newBox);
                page.SetCropBox(newBox);
                new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdfDoc)
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
                            var imageBytes = imageXObject.GetImageBytes();
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
                                    _imageService.SaveWithDetectedFormat(imageBytes, baseOutputPath);
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
                                    _imageService.SaveBytesAsJpeg(imageBytes, $"{baseOutputPath}.jpg");
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
