using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFiumSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfDocument = PdfSharp.Pdf.PdfDocument;
using PdfiumDocument = PDFiumSharp.PdfDocument;

namespace PdfTool.App.Services;

public class PdfCompressionService : IPdfCompressionService
{
    private static readonly object RenderSync = new();
    private readonly IPdfCompressionInspectorService _compressionInspectorService;
    private readonly IPdfDocumentInspectorService _documentInspectorService;
    private readonly IPdfImageObjectOptimizationService _imageObjectOptimizationService;
    private readonly IAppLogger _logger;

    public PdfCompressionService(
        IPdfCompressionInspectorService compressionInspectorService,
        IPdfDocumentInspectorService documentInspectorService,
        IPdfImageObjectOptimizationService imageObjectOptimizationService,
        IAppLogger logger)
    {
        _compressionInspectorService = compressionInspectorService;
        _documentInspectorService = documentInspectorService;
        _imageObjectOptimizationService = imageObjectOptimizationService;
        _logger = logger;
    }

    public OperationResult Compress(PdfCompressionOptions options)
    {
        string? tempOutputPath = null;

        try
        {
            var effectivePassword = options.GetEffectivePassword();
            _logger.LogInfo($"Compression start. Input='{options.InputPath}', Output='{options.OutputPath}', ProfileLevel={options.CompressionLevel}.");
            if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFile(options.OutputPath, out var outputError))
            {
                return OperationResult.Fail(outputError);
            }

            if (string.Equals(options.InputPath, options.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Output PDF must be different from input PDF.");
            }

            var inspection = _compressionInspectorService.Inspect(options.InputPath, effectivePassword);
            if (!inspection.Success)
            {
                return OperationResult.Fail(string.IsNullOrWhiteSpace(inspection.Message)
                    ? "Compression preflight failed for this PDF."
                    : inspection.Message);
            }

            var plan = BuildCompressionPlan(options.CompressionLevel, inspection);
            tempOutputPath = CreateTemporaryOutputPath(options.OutputPath);
            var sourceAnnotationCounts = CountAnnotations(options.InputPath, effectivePassword);
            var runSummary = plan.AllowPageRasterization
                ? ExecuteStrongCompression(options.InputPath, tempOutputPath, inspection, plan, effectivePassword)
                : ExecuteObjectLevelImageOptimization(options, tempOutputPath, inspection, plan, effectivePassword);
            var validation = ValidateCompressedOutput(
                options.InputPath,
                tempOutputPath,
                inspection.PageCount,
                plan,
                sourceAnnotationCounts);
            if (!validation.Success)
            {
                TryDeleteFile(tempOutputPath);
                return OperationResult.Fail(validation.Message);
            }

            if (File.Exists(options.OutputPath))
            {
                File.Delete(options.OutputPath);
            }

            File.Move(tempOutputPath, options.OutputPath, true);
            tempOutputPath = null;
            var inputSize = new FileInfo(options.InputPath).Length;
            var outputSize = new FileInfo(options.OutputPath).Length;
            var reductionPercent = inputSize > 0
                ? Math.Max(0, (inputSize - outputSize) * 100.0 / inputSize)
                : 0;

            var result = OperationResult.Ok(
                $"Compression completed with {plan.Label}. Size: {FormatBytes(inputSize)} -> {FormatBytes(outputSize)} ({reductionPercent:0.#}% smaller). {plan.Guidance}",
                options.OutputPath);
            result.CompressionRunSummary = runSummary;
            _logger.LogInfo($"Compression success. Output='{options.OutputPath}', Saved={reductionPercent:0.#}%.");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compression failed. Input='{options.InputPath}', Output='{options.OutputPath}'.", ex);
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Incorrect password or owner password required for this protected PDF.");
            }

            return OperationResult.Fail($"Compression failed: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempOutputPath))
            {
                TryDeleteFile(tempOutputPath);
            }
        }
    }

    private PdfCompressionRunSummary ExecuteObjectLevelImageOptimization(
        PdfCompressionOptions options,
        string outputPath,
        PdfCompressionInspectionResult inspection,
        PdfCompressionPlan plan,
        string? password)
    {
        using var document = string.IsNullOrWhiteSpace(password)
            ? PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Modify)
            : PdfReader.Open(options.InputPath, password, PdfDocumentOpenMode.Modify, new PdfReaderOptions());

        ApplyCompressionProfile(document, plan);
        var imageSummary = plan.OptimizeImageObjects
            ? _imageObjectOptimizationService.OptimizeImages(document, plan)
            : new PdfImageOptimizationSummary();

        document.Save(outputPath);

        return new PdfCompressionRunSummary
        {
            MethodUsed = imageSummary.OptimizedImageCount > 0
                ? $"{plan.Label}: embedded image optimization"
                : $"{plan.Label}: structure optimization only",
            TotalPageCount = inspection.PageCount,
            OptimizedImageCount = imageSummary.OptimizedImageCount,
            SkippedImageCount = imageSummary.SkippedImageCount,
            RasterizedPageCount = 0,
            GrayscalePageCount = 0,
            TextVectorPreserved = true,
            LinksPreserved = true,
            ColorMode = "Original color preserved",
            Warning = imageSummary.OptimizedImageCount == 0
                ? "No safe embedded JPEG images were small enough to optimize meaningfully."
                : string.Empty
        };
    }

    private PdfCompressionRunSummary ExecuteStrongCompression(
        string inputPath,
        string outputPath,
        PdfCompressionInspectionResult inspection,
        PdfCompressionPlan plan,
        string? password)
    {
        using var source = string.IsNullOrWhiteSpace(password)
            ? PdfReader.Open(inputPath, PdfDocumentOpenMode.Import)
            : PdfReader.Open(inputPath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());
        using var target = new PdfDocument();
        ApplyCompressionProfile(target, plan);

        var inputSize = Math.Max(1, new FileInfo(inputPath).Length);
        var totalArea = 0d;
        for (var index = 0; index < source.PageCount; index++)
        {
            totalArea += source.Pages[index].Width.Point * source.Pages[index].Height.Point;
        }

        var imageStreams = new List<MemoryStream>();
        var rasterizedPageCount = 0;
        var grayscalePageCount = 0;

        lock (RenderSync)
        {
            using var renderLease = PdfiumFileHelper.OpenDocument(inputPath, password);
            var renderDocument = renderLease.Document;
            var pageCount = Math.Min(source.PageCount, renderDocument.Pages.Count);

            if (pageCount == 0)
            {
                throw new InvalidOperationException("Input PDF has no pages to compress.");
            }

            for (var index = 0; index < pageCount; index++)
            {
                var sourcePage = source.Pages[index];
                var renderPage = renderDocument.Pages[index];
                var pageAnalysis = inspection.Pages.FirstOrDefault(page => page.PageNumber == index + 1);
                if (!plan.AllowPageRasterization || !ShouldConsiderRasterizing(pageAnalysis, plan))
                {
                    target.AddPage(sourcePage);
                    continue;
                }

                var targetDpi = ResolveTargetDpi(pageAnalysis, plan);
                var jpegQuality = ResolveJpegQuality(pageAnalysis, plan);
                var pixelWidth = Math.Max(1, (int)Math.Round(sourcePage.Width.Point / 72d * targetDpi));
                var pixelHeight = Math.Max(1, (int)Math.Round(sourcePage.Height.Point / 72d * targetDpi));
                var renderedImage = RenderingExtensionsWpf.CreateImageSource(
                    renderPage,
                    pixelWidth,
                    pixelHeight,
                    true,
                    renderPage.Orientation,
                    PDFiumSharp.RenderingFlags.Annotations | PDFiumSharp.RenderingFlags.LcdText);

                if (renderedImage is not BitmapSource bitmapSource)
                {
                    target.AddPage(sourcePage);
                    continue;
                }

                bitmapSource = FlattenToWhiteBackground(bitmapSource);
                var useGrayscale = plan.PreferGrayscaleForLowColorPages && (pageAnalysis?.PreferGrayscale ?? false);
                var jpegStream = EncodeBitmapToJpeg(
                    bitmapSource,
                    jpegQuality,
                    useGrayscale);
                var pageArea = sourcePage.Width.Point * sourcePage.Height.Point;
                var pageBudget = Math.Max(1, (long)Math.Round(inputSize * (pageArea / Math.Max(1d, totalArea))));

                if (ShouldRasterizePage(pageAnalysis, plan, jpegStream.Length, pageBudget))
                {
                    rasterizedPageCount++;
                    if (useGrayscale)
                    {
                        grayscalePageCount++;
                    }

                    var outputPage = target.AddPage();
                    outputPage.Width = sourcePage.Width;
                    outputPage.Height = sourcePage.Height;
                    imageStreams.Add(jpegStream);
                    using var xImage = XImage.FromStream(jpegStream);
                    using var graphics = XGraphics.FromPdfPage(outputPage);
                    graphics.DrawImage(xImage, 0, 0, outputPage.Width.Point, outputPage.Height.Point);
                }
                else
                {
                    jpegStream.Dispose();
                    target.AddPage(sourcePage);
                }
            }
        }

        var imageSummary = _imageObjectOptimizationService.OptimizeImages(target, plan);

        try
        {
            target.Save(outputPath);
        }
        finally
        {
            foreach (var stream in imageStreams)
            {
                stream.Dispose();
            }
        }

        return new PdfCompressionRunSummary
        {
            MethodUsed = rasterizedPageCount > 0
                ? plan.RasterizeAllPages
                    ? $"{plan.Label}: full-page rasterization + embedded image optimization"
                    : $"{plan.Label}: selective scan-page rasterization + embedded image optimization"
                : $"{plan.Label}: embedded image optimization",
            OptimizedImageCount = imageSummary.OptimizedImageCount,
            SkippedImageCount = imageSummary.SkippedImageCount,
            RasterizedPageCount = rasterizedPageCount,
            TotalPageCount = inspection.PageCount,
            GrayscalePageCount = grayscalePageCount,
            TextVectorPreserved = rasterizedPageCount == 0,
            LinksPreserved = rasterizedPageCount == 0,
            ColorMode = grayscalePageCount > 0
                ? grayscalePageCount == rasterizedPageCount
                    ? "Grayscale output pages"
                    : "Color + grayscale on low-color pages"
                : rasterizedPageCount > 0
                    ? "Color only"
                    : "Original color preserved",
            Warning = rasterizedPageCount > 0
                ? plan.RasterizeAllPages
                    ? "Extreme compression rasterized every page. Searchable text, links, annotations, and vector sharpness may be lost or reduced."
                    : "Strong compression rasterized some pages. Searchable text, links, annotations, and vector sharpness may be reduced on those pages."
                : string.Empty
        };
    }

    private static void ApplyCompressionProfile(PdfDocument document, PdfCompressionPlan plan)
    {
        var options = document.Options;
        options.NoCompression = false;
        options.CompressContentStreams = true;

        switch (plan.Strategy)
        {
            case PdfCompressionStrategy.Safe:
                options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                options.EnableCcittCompressionForBilevelImages = false;
                options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
                break;
            case PdfCompressionStrategy.Balanced:
                options.FlateEncodeMode = PdfFlateEncodeMode.Default;
                options.EnableCcittCompressionForBilevelImages = true;
                options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
                break;
            case PdfCompressionStrategy.Extreme:
                options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                options.EnableCcittCompressionForBilevelImages = true;
                options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
                break;
            default:
                options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                options.EnableCcittCompressionForBilevelImages = true;
                options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
                break;
        }
    }

    private static PdfCompressionPlan BuildCompressionPlan(int compressionLevel, PdfCompressionInspectionResult inspection)
    {
        var normalizedLevel = Math.Clamp(compressionLevel, 0, 100);
        if (normalizedLevel <= 15)
        {
            var factor = normalizedLevel / 15d;
            return new PdfCompressionPlan
            {
                Strategy = PdfCompressionStrategy.Safe,
                Label = "Safe",
                Guidance = inspection.Guidance,
                OptimizeImageObjects = true,
                AllowPageRasterization = false,
                MixedPageTargetDpi = InterpolateInt(300, 274, factor),
                ImageHeavyPageTargetDpi = InterpolateInt(270, 244, factor),
                MixedPageJpegQuality = InterpolateInt(94, 90, factor),
                ImageHeavyPageJpegQuality = InterpolateInt(90, 84, factor),
                PreferGrayscaleForLowColorPages = false,
                RequireScanLikePages = false,
                MixedPageBudgetRatio = InterpolateDouble(0.90, 0.917, factor),
                ImageHeavyPageBudgetRatio = InterpolateDouble(1.04, 1.07, factor),
                MinimumSavingsRatio = 0.04,
                MaxImageDpi = 300,
                ImageJpegQuality = 85,
                MinimumImageJpegQuality = 85,
                TargetImageSavingsRatio = 0.50,
                MaxImagePixelDimension = 3200,
                AllowGrayscaleImageObjects = false
            };
        }

        if (normalizedLevel < 70)
        {
            var factor = (normalizedLevel - 15d) / 54d;
            return new PdfCompressionPlan
            {
                Strategy = normalizedLevel <= 35
                    ? PdfCompressionStrategy.Safe
                    : PdfCompressionStrategy.Balanced,
                Label = normalizedLevel <= 35
                    ? "Safe"
                    : "Balanced",
                Guidance = inspection.Guidance,
                OptimizeImageObjects = true,
                AllowPageRasterization = false,
                MixedPageTargetDpi = InterpolateInt(274, 200, factor),
                ImageHeavyPageTargetDpi = InterpolateInt(244, 170, factor),
                MixedPageJpegQuality = InterpolateInt(90, 84, factor),
                ImageHeavyPageJpegQuality = InterpolateInt(84, 72, factor),
                PreferGrayscaleForLowColorPages = false,
                RequireScanLikePages = false,
                MixedPageBudgetRatio = InterpolateDouble(0.917, 0.965, factor),
                ImageHeavyPageBudgetRatio = InterpolateDouble(1.07, 1.14, factor),
                MinimumSavingsRatio = normalizedLevel <= 35 ? 0.04 : 0.02,
                MaxImageDpi = InterpolateInt(260, 180, factor),
                ImageJpegQuality = normalizedLevel <= 35 ? 85 : 70,
                MinimumImageJpegQuality = normalizedLevel <= 35 ? 85 : 70,
                TargetImageSavingsRatio = 0.50,
                MaxImagePixelDimension = normalizedLevel <= 35 ? 3200 : 2400,
                AllowGrayscaleImageObjects = false
            };
        }

        if (normalizedLevel >= 90)
        {
            return new PdfCompressionPlan
            {
                Strategy = PdfCompressionStrategy.Extreme,
                Label = "Extreme",
                Guidance = inspection.Guidance,
                OptimizeImageObjects = true,
                AllowPageRasterization = true,
                RasterizeAllPages = true,
                MixedPageTargetDpi = 190,
                ImageHeavyPageTargetDpi = 175,
                MixedPageJpegQuality = 68,
                ImageHeavyPageJpegQuality = 62,
                PreferGrayscaleForLowColorPages = false,
                RequireScanLikePages = false,
                MixedPageBudgetRatio = 1.20,
                ImageHeavyPageBudgetRatio = 1.28,
                MinimumSavingsRatio = 0.01,
                MaxImageDpi = 150,
                ImageJpegQuality = 60,
                MinimumImageJpegQuality = 54,
                TargetImageSavingsRatio = 0.75,
                MaxImagePixelDimension = 1800,
                AllowGrayscaleImageObjects = false
            };
        }

        return new PdfCompressionPlan
        {
            Strategy = PdfCompressionStrategy.Strong,
            Label = "Strong",
            Guidance = inspection.Guidance,
            OptimizeImageObjects = true,
            AllowPageRasterization = true,
            MixedPageTargetDpi = 150,
            ImageHeavyPageTargetDpi = 130,
            MixedPageJpegQuality = 62,
            ImageHeavyPageJpegQuality = 56,
            PreferGrayscaleForLowColorPages = false,
            RequireScanLikePages = true,
            MixedPageBudgetRatio = 1.08,
            ImageHeavyPageBudgetRatio = 1.20,
            MinimumSavingsRatio = 0.015,
            MaxImageDpi = 150,
            ImageJpegQuality = 60,
            MinimumImageJpegQuality = 54,
            TargetImageSavingsRatio = 0.68,
            MaxImagePixelDimension = 1800,
            AllowGrayscaleImageObjects = false
        };
    }

    private static bool ShouldConsiderRasterizing(PdfCompressionPageAnalysis? pageAnalysis, PdfCompressionPlan plan)
    {
        if (!plan.AllowPageRasterization || pageAnalysis == null)
        {
            return false;
        }

        if (plan.RasterizeAllPages)
        {
            return true;
        }

        if (plan.RequireScanLikePages && !IsStrongRasterizationCandidate(pageAnalysis))
        {
            return false;
        }

        return pageAnalysis.Category != PdfCompressionPageCategory.TextOrVector;
    }

    private static bool IsStrongRasterizationCandidate(PdfCompressionPageAnalysis pageAnalysis)
        => pageAnalysis.IsScanLike
           || (pageAnalysis.Category == PdfCompressionPageCategory.ImageHeavy
               && pageAnalysis.InkCoverage >= 0.10
               && pageAnalysis.EdgeDensity <= 0.08);

    private static int ResolveTargetDpi(PdfCompressionPageAnalysis? pageAnalysis, PdfCompressionPlan plan)
    {
        return pageAnalysis?.Category == PdfCompressionPageCategory.ImageHeavy
            ? plan.ImageHeavyPageTargetDpi
            : plan.MixedPageTargetDpi;
    }

    private static int ResolveJpegQuality(PdfCompressionPageAnalysis? pageAnalysis, PdfCompressionPlan plan)
    {
        return pageAnalysis?.Category == PdfCompressionPageCategory.ImageHeavy
            ? plan.ImageHeavyPageJpegQuality
            : plan.MixedPageJpegQuality;
    }

    private static bool ShouldRasterizePage(
        PdfCompressionPageAnalysis? pageAnalysis,
        PdfCompressionPlan plan,
        long rasterizedPageBytes,
        long pageBudget)
    {
        if (!ShouldConsiderRasterizing(pageAnalysis, plan))
        {
            return false;
        }

        var allowedBudgetRatio = pageAnalysis!.Category == PdfCompressionPageCategory.ImageHeavy
            ? plan.ImageHeavyPageBudgetRatio
            : plan.MixedPageBudgetRatio;
        var savingsRatio = 1d - (rasterizedPageBytes / (double)Math.Max(1, pageBudget));
        return rasterizedPageBytes <= pageBudget * allowedBudgetRatio
               && savingsRatio >= plan.MinimumSavingsRatio;
    }

    private PdfCompressionValidationResult ValidateCompressedOutput(
        string inputPath,
        string outputPath,
        int expectedPageCount,
        PdfCompressionPlan plan,
        AnnotationCounts sourceAnnotationCounts)
    {
        if (!File.Exists(outputPath))
        {
            return new PdfCompressionValidationResult
            {
                Success = false,
                Message = "Compression validation failed because no output file was created."
            };
        }

        var outputSize = new FileInfo(outputPath).Length;
        if (outputSize <= 0)
        {
            return new PdfCompressionValidationResult
            {
                Success = false,
                Message = "Compression validation failed because the output PDF is empty."
            };
        }

        var inputSize = new FileInfo(inputPath).Length;
        if (outputSize >= inputSize)
        {
            return new PdfCompressionValidationResult
            {
                Success = false,
                Message = "Compression would not reduce file size for this PDF. Original file was kept."
            };
        }

        var documentInfo = _documentInspectorService.Inspect(outputPath);
        if (!documentInfo.Exists || !documentInfo.IsPdf || !documentInfo.CanReadContents)
        {
            return new PdfCompressionValidationResult
            {
                Success = false,
                Message = string.IsNullOrWhiteSpace(documentInfo.StatusMessage)
                    ? "Compression validation failed because the output PDF could not be opened."
                    : documentInfo.StatusMessage
            };
        }

        if (documentInfo.PageCount != expectedPageCount)
        {
            return new PdfCompressionValidationResult
            {
                Success = false,
                Message = $"Compression validation failed because page count changed from {expectedPageCount} to {documentInfo.PageCount}."
            };
        }

        if (!plan.AllowPageRasterization)
        {
            var outputAnnotationCounts = CountAnnotations(outputPath, null);
            if (outputAnnotationCounts.TotalAnnotations != sourceAnnotationCounts.TotalAnnotations)
            {
                return new PdfCompressionValidationResult
                {
                    Success = false,
                    Message = "Compression changed annotations. Original file was kept."
                };
            }

            if (outputAnnotationCounts.LinkAnnotations != sourceAnnotationCounts.LinkAnnotations)
            {
                return new PdfCompressionValidationResult
                {
                    Success = false,
                    Message = "Compression changed link annotations. Original file was kept."
                };
            }
        }

        lock (RenderSync)
        {
            using var renderLease = PdfiumFileHelper.OpenDocument(outputPath);
            var renderDocument = renderLease.Document;
            if (renderDocument.Pages.Count != expectedPageCount)
            {
                return new PdfCompressionValidationResult
                {
                    Success = false,
                    Message = "Compression validation failed because the rendered page count does not match the source PDF."
                };
            }

            foreach (var sampleIndex in BuildSamplePageIndexes(expectedPageCount))
            {
                var page = renderDocument.Pages[sampleIndex];
                var preview = RenderingExtensionsWpf.CreateImageSource(
                    page,
                    180,
                    240,
                    true,
                    page.Orientation,
                    PDFiumSharp.RenderingFlags.Annotations | PDFiumSharp.RenderingFlags.LcdText);

                if (preview is not BitmapSource bitmapSource)
                {
                    return new PdfCompressionValidationResult
                    {
                        Success = false,
                        Message = $"Compression validation failed while rendering page {sampleIndex + 1}."
                    };
                }

                _ = FlattenToWhiteBackground(bitmapSource);
            }
        }

        return new PdfCompressionValidationResult
        {
            Success = true,
            OutputSizeBytes = outputSize,
            Message = "Validated successfully."
        };
    }

    private static AnnotationCounts CountAnnotations(string pdfPath, string? password)
    {
        try
        {
            using var document = string.IsNullOrWhiteSpace(password)
                ? PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import)
                : PdfReader.Open(pdfPath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());

            var counts = new AnnotationCounts();
            foreach (var page in document.Pages)
            {
                var annotations = page.Elements.GetArray("/Annots");
                if (annotations == null)
                {
                    continue;
                }

                foreach (var item in annotations.Elements)
                {
                    var annotation = ResolveAnnotationDictionary(item);
                    if (annotation == null)
                    {
                        continue;
                    }

                    counts.TotalAnnotations++;
                    if (annotation.Elements.GetName("/Subtype") == "/Link")
                    {
                        counts.LinkAnnotations++;
                    }
                }
            }

            return counts;
        }
        catch
        {
            return new AnnotationCounts();
        }
    }

    private static PdfDictionary? ResolveAnnotationDictionary(PdfItem item)
    {
        if (item is PdfReference reference)
        {
            return reference.Value as PdfDictionary;
        }

        return item as PdfDictionary;
    }

    private static MemoryStream EncodeBitmapToJpeg(BitmapSource bitmapSource, int jpegQuality, bool convertToGrayscale)
    {
        BitmapSource source = bitmapSource;
        if (convertToGrayscale)
        {
            source = new FormatConvertedBitmap(bitmapSource, PixelFormats.Gray8, null, 0);
        }

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = Math.Clamp(jpegQuality, 1, 100)
        };

        encoder.Frames.Add(BitmapFrame.Create(source));

        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double length = bytes;

        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }

    private static int InterpolateInt(int start, int end, double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return (int)Math.Round(start + ((end - start) * factor));
    }

    private static double InterpolateDouble(double start, double end, double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return start + ((end - start) * factor);
    }

    private static BitmapSource FlattenToWhiteBackground(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            context.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }

        var bitmap = new RenderTargetBitmap(
            source.PixelWidth,
            source.PixelHeight,
            Math.Max(96, source.DpiX),
            Math.Max(96, source.DpiY),
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static string CreateTemporaryOutputPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Output path is invalid.");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.compressing.tmp.pdf");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static IEnumerable<int> BuildSamplePageIndexes(int pageCount)
    {
        if (pageCount <= 0)
        {
            yield break;
        }

        yield return 0;

        if (pageCount > 2)
        {
            yield return pageCount / 2;
        }

        if (pageCount > 1)
        {
            yield return pageCount - 1;
        }
    }

    private sealed class AnnotationCounts
    {
        public int TotalAnnotations { get; set; }
        public int LinkAnnotations { get; set; }
    }
}
