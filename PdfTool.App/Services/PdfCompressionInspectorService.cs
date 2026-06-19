using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using PDFiumSharp;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfCompressionInspectorService : IPdfCompressionInspectorService
{
    private static readonly object RenderSync = new();
    private readonly IPdfDocumentInspectorService _documentInspectorService;

    public PdfCompressionInspectorService(IPdfDocumentInspectorService documentInspectorService)
    {
        _documentInspectorService = documentInspectorService;
    }

    public PdfCompressionInspectionResult Inspect(string filePath, string? password = null)
    {
        var documentInfo = _documentInspectorService.Inspect(filePath, password);
        var inspection = new PdfCompressionInspectionResult
        {
            FilePath = documentInfo.FilePath,
            FileName = documentInfo.FileName,
            FileSizeBytes = documentInfo.FileSizeBytes,
            Exists = documentInfo.Exists,
            IsPdf = documentInfo.IsPdf,
            IsEncrypted = documentInfo.IsEncrypted,
            HasOwnerPermissions = documentInfo.HasOwnerPermissions,
            RequiresPassword = documentInfo.RequiresPassword,
            IsPasswordIncorrect = documentInfo.IsPasswordIncorrect,
            CanReadContents = documentInfo.CanReadContents,
            PageCount = documentInfo.PageCount ?? 0,
            Message = documentInfo.StatusMessage
        };

        if (!documentInfo.Exists
            || !documentInfo.IsPdf
            || documentInfo.RequiresPassword
            || documentInfo.IsPasswordIncorrect
            || !documentInfo.CanReadContents
            || (documentInfo.IsEncrypted && !documentInfo.HasOwnerPermissions))
        {
            inspection.Message = documentInfo.RequiresPassword
                ? "Owner password required to inspect and compress this PDF."
                : documentInfo.IsPasswordIncorrect
                    ? "Incorrect password. Enter the correct owner password to continue."
                    : documentInfo.IsEncrypted && !documentInfo.HasOwnerPermissions
                        ? "Owner password is required to compress this protected PDF."
                        : documentInfo.StatusMessage;
            inspection.RiskWarnings = BuildWarnings(inspection);
            inspection.Success = false;
            return inspection;
        }

        try
        {
            var pageAnalyses = new List<PdfCompressionPageAnalysis>();

            lock (RenderSync)
            {
                using var source = string.IsNullOrWhiteSpace(password)
                    ? PdfReader.Open(filePath, PdfDocumentOpenMode.Import)
                    : PdfReader.Open(filePath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());
                using var renderLease = PdfiumFileHelper.OpenDocument(filePath, password);
                var renderDocument = renderLease.Document;
                var pageCount = Math.Min(source.PageCount, renderDocument.Pages.Count);

                for (var index = 0; index < pageCount; index++)
                {
                    var sourcePage = source.Pages[index];
                    var renderPage = renderDocument.Pages[index];
                    var pixelWidth = Math.Max(180, (int)Math.Round(sourcePage.Width.Point / 72d * 72d));
                    var pixelHeight = Math.Max(180, (int)Math.Round(sourcePage.Height.Point / 72d * 72d));

                    var imageSource = RenderingExtensionsWpf.CreateImageSource(
                        renderPage,
                        pixelWidth,
                        pixelHeight,
                        true,
                        renderPage.Orientation,
                        PDFiumSharp.RenderingFlags.Annotations | PDFiumSharp.RenderingFlags.LcdText);

                    if (imageSource is not BitmapSource bitmapSource)
                    {
                        continue;
                    }

                    pageAnalyses.Add(AnalyzePage(FlattenToWhiteBackground(bitmapSource), index + 1));
                }
            }

            inspection.Pages = pageAnalyses;
            inspection.TextVectorPageCount = pageAnalyses.Count(page => page.Category == PdfCompressionPageCategory.TextOrVector);
            inspection.MixedPageCount = pageAnalyses.Count(page => page.Category == PdfCompressionPageCategory.Mixed);
            inspection.ImageHeavyPageCount = pageAnalyses.Count(page => page.Category == PdfCompressionPageCategory.ImageHeavy);
            inspection.SuggestedStrategy = SuggestStrategy(inspection);
            inspection.Guidance = BuildGuidance(inspection);
            inspection.RiskWarnings = BuildWarnings(inspection);
            inspection.Success = pageAnalyses.Count == inspection.PageCount;
            inspection.Message = inspection.Success
                ? "Inspection completed."
                : "Inspection could not analyze every page in this PDF.";
            return inspection;
        }
        catch (Exception ex)
        {
            inspection.Success = false;
            inspection.Message = $"Inspection failed: {ex.Message}";
            return inspection;
        }
    }

    private static PdfCompressionPageAnalysis AnalyzePage(BitmapSource bitmapSource, int pageNumber)
    {
        var formatted = bitmapSource.Format == PixelFormats.Bgra32
            ? bitmapSource
            : new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
        var stride = formatted.PixelWidth * 4;
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        var width = formatted.PixelWidth;
        var height = formatted.PixelHeight;
        var totalPixels = Math.Max(1, width * height);
        var luminance = new byte[totalPixels];
        var darkPixels = 0;
        double sum = 0;
        double sumSquares = 0;
        double colorfulnessSum = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var value = (byte)Math.Clamp((int)Math.Round(red * 0.2126 + green * 0.7152 + blue * 0.0722), 0, 255);
                luminance[y * width + x] = value;
                sum += value;
                sumSquares += value * value;
                colorfulnessSum += (Math.Abs(red - green) + Math.Abs(red - blue) + Math.Abs(green - blue)) / 3d;

                if (value < 245)
                {
                    darkPixels++;
                }
            }
        }

        long edgeCount = 0;
        long comparisonCount = 0;
        const int edgeThreshold = 22;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var current = luminance[index];

                if (x + 1 < width)
                {
                    comparisonCount++;
                    if (Math.Abs(current - luminance[index + 1]) >= edgeThreshold)
                    {
                        edgeCount++;
                    }
                }

                if (y + 1 < height)
                {
                    comparisonCount++;
                    if (Math.Abs(current - luminance[index + width]) >= edgeThreshold)
                    {
                        edgeCount++;
                    }
                }
            }
        }

        var averageBrightness = sum / totalPixels;
        var variance = Math.Max(0, (sumSquares / totalPixels) - (averageBrightness * averageBrightness));
        var inkCoverage = darkPixels / (double)totalPixels;
        var edgeDensity = comparisonCount == 0 ? 0 : edgeCount / (double)comparisonCount;
        var colorfulness = colorfulnessSum / totalPixels;

        var category = ClassifyPage(inkCoverage, edgeDensity, variance);
        var isScanLike = colorfulness < 16
                         && inkCoverage >= 0.05
                         && edgeDensity <= 0.05
                         && averageBrightness < 250;
        return new PdfCompressionPageAnalysis
        {
            PageNumber = pageNumber,
            Category = category,
            InkCoverage = inkCoverage,
            EdgeDensity = edgeDensity,
            AverageBrightness = averageBrightness,
            Colorfulness = colorfulness,
            PreferGrayscale = colorfulness < 10,
            IsScanLike = isScanLike
        };
    }

    private static PdfCompressionPageCategory ClassifyPage(double inkCoverage, double edgeDensity, double variance)
    {
        var looksLikeText = inkCoverage <= 0.14 && edgeDensity >= 0.045;
        var looksLikeImage = inkCoverage >= 0.20
                             || (inkCoverage >= 0.12 && edgeDensity <= 0.035 && variance >= 500)
                             || (inkCoverage >= 0.16 && edgeDensity <= 0.045 && variance >= 380);

        if (looksLikeImage)
        {
            return PdfCompressionPageCategory.ImageHeavy;
        }

        if (looksLikeText)
        {
            return PdfCompressionPageCategory.TextOrVector;
        }

        return PdfCompressionPageCategory.Mixed;
    }

    private static PdfCompressionStrategy SuggestStrategy(PdfCompressionInspectionResult inspection)
    {
        if (inspection.ImageHeavyPageCount >= Math.Max(1, inspection.PageCount * 0.65))
        {
            return PdfCompressionStrategy.Strong;
        }

        if (inspection.ImageHeavyPageCount > 0 || inspection.MixedPageCount >= Math.Max(1, inspection.PageCount / 3))
        {
            return PdfCompressionStrategy.Balanced;
        }

        return PdfCompressionStrategy.Safe;
    }

    private static string BuildGuidance(PdfCompressionInspectionResult inspection)
    {
        if (inspection.ImageHeavyPageCount >= Math.Max(1, inspection.PageCount * 0.65))
        {
            return "This PDF appears scan-heavy. Strong compression should save the most space, while Safe and Balanced will preserve structure.";
        }

        if (inspection.ImageHeavyPageCount > 0)
        {
            return "This PDF mixes text/vector pages with heavier images. Balanced compression will optimize embedded images while preserving structure.";
        }

        return "This PDF appears mostly text or vector based. Safe compression should preserve structure; size reduction may be limited.";
    }

    private static IReadOnlyList<string> BuildWarnings(PdfCompressionInspectionResult inspection)
    {
        var warnings = new List<string>();

        if (!inspection.Exists)
        {
            warnings.Add("Input file does not exist.");
            return warnings;
        }

        if (!inspection.IsPdf)
        {
            warnings.Add("Input file is not recognized as a valid PDF.");
            return warnings;
        }

        if (inspection.RequiresPassword)
        {
            warnings.Add("This PDF is protected. Enter the owner password to inspect and compress it.");
        }
        else if (inspection.IsPasswordIncorrect)
        {
            warnings.Add("Incorrect password. Enter the correct owner password to continue.");
        }
        else if (inspection.IsEncrypted && !inspection.HasOwnerPermissions)
        {
            warnings.Add("Owner password is required to compress this protected PDF.");
        }

        if (!inspection.CanReadContents && !inspection.RequiresPassword)
        {
            warnings.Add("This PDF could not be fully inspected. Compression may fail.");
        }

        if (inspection.PageCount > 0 && inspection.TextVectorPageCount == inspection.PageCount)
        {
            warnings.Add("This PDF is mostly text/vector. Size reduction may be limited, especially with Safe or Balanced compression.");
        }
        else if (inspection.MixedPageCount > 0 && inspection.ImageHeavyPageCount == 0)
        {
            warnings.Add("Mixed layout pages are preserved in Safe/Balanced. Strong compression may reduce searchability if pages are rasterized.");
        }
        else if (inspection.MixedPageCount > 0 && inspection.ImageHeavyPageCount > 0)
        {
            warnings.Add("This PDF mixes scan-heavy and layout pages. Review output carefully when using Strong compression.");
        }

        return warnings;
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
}
