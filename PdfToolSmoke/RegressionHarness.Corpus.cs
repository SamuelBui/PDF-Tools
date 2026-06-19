using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfTool.App.Models;

namespace PdfToolSmoke;

internal sealed partial class RegressionHarness
{
    private GeneratedCorpus GenerateCorpus()
    {
        Directory.CreateDirectory(_corpusRoot);
        Directory.CreateDirectory(_inputRoot);
        Directory.CreateDirectory(_derivedRoot);
        Directory.CreateDirectory(_assetsRoot);
        Directory.CreateDirectory(_resultsRoot);

        var descriptors = new List<CorpusFileDescriptor>();

        var textReportPath = Path.Combine(_inputRoot, "01-text-report.pdf");
        CreateTextReportPdf(textReportPath, "Quarterly Status Report", 3);
        descriptors.Add(CreateDescriptor("text-report", textReportPath, "Text-heavy document for protect/merge checks.", ["text", "report"], null, null));

        var vectorPath = Path.Combine(_inputRoot, "02-vector-diagrams.pdf");
        CreateVectorDiagramPdf(vectorPath, "Network Diagram Pack", 2);
        descriptors.Add(CreateDescriptor("vector-diagrams", vectorPath, "Vector shapes and labels for merge/reorder/rotate checks.", ["vector", "diagram"], null, null));

        var mixedPath = Path.Combine(_inputRoot, "03-mixed-brochure.pdf");
        CreateMixedBrochurePdf(mixedPath, "Training Brochure", 3);
        descriptors.Add(CreateDescriptor("mixed-brochure", mixedPath, "Mixed text and image document for split/compress checks.", ["mixed", "brochure"], null, null));

        var scanColorPath = Path.Combine(_inputRoot, "04-scan-color.pdf");
        CreateImageHeavyPdf(scanColorPath, "Color Scan Sample", 4, lowColor: false, useBmp: false);
        descriptors.Add(CreateDescriptor("scan-color", scanColorPath, "Image-heavy color scan sample for compression.", ["scan", "color", "image-heavy"], null, null));

        var scanLowColorPath = Path.Combine(_inputRoot, "05-scan-lowcolor.pdf");
        CreateImageHeavyPdf(scanLowColorPath, "Low Color Scan Sample", 4, lowColor: true, useBmp: true);
        descriptors.Add(CreateDescriptor("scan-lowcolor", scanLowColorPath, "Image-heavy low-color scan sample for grayscale compression.", ["scan", "low-color", "image-heavy"], null, null));

        var restrictedPath = Path.Combine(_derivedRoot, "06-protected-restricted.pdf");
        var protectResult = _protectionService.Protect(new PdfProtectionOptions
        {
            InputPath = textReportPath,
            OutputPath = restrictedPath,
            UserPassword = "UserPass!123",
            OwnerPassword = "OwnerPass!123",
            AllowPrint = false,
            AllowFullQualityPrint = false,
            AllowModifyDocument = false,
            AllowExtractContent = false,
            AllowAnnotations = false,
            AllowFormsFill = false,
            AllowAssembleDocument = false
        });

        if (!protectResult.Success)
        {
            throw new InvalidOperationException($"Could not generate protected corpus file: {protectResult.Message}");
        }

        descriptors.Add(CreateDescriptor("protected-restricted", restrictedPath, "Protected PDF with user and owner passwords plus restricted permissions.", ["protected", "restricted"], "UserPass!123", "OwnerPass!123"));

        var invalidPdfPath = Path.Combine(_inputRoot, "99-invalid.pdf");
        DeleteFileIfExists(invalidPdfPath);
        File.WriteAllText(invalidPdfPath, "This is not a valid PDF document.");
        descriptors.Add(new CorpusFileDescriptor
        {
            Id = "invalid-pseudo-pdf",
            RelativePath = GetRelativePath(invalidPdfPath),
            AbsolutePath = invalidPdfPath,
            Description = "Plain text file with .pdf extension for invalid-input checks.",
            Tags = ["invalid", "negative"],
            PageCount = 0,
            FileSizeBytes = new FileInfo(invalidPdfPath).Length
        });

        var manifest = new CorpusManifest
        {
            GeneratedAtLocal = DateTime.Now,
            CorpusRoot = _corpusRoot,
            Files = descriptors.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToList()
        };

        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(manifest, _jsonOptions));
        return new GeneratedCorpus(manifest, descriptors.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase));
    }

    private void CreateTextReportPdf(string path, string title, int pages)
    {
        DeleteFileIfExists(path);
        using var document = new PdfDocument();
        document.Info.Title = title;

        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(230, 240, 250)), 40, 30, page.Width.Point - 80, 28, 10, 10);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(60, 92, 140)), 56, 37, 220, 9, 4, 4);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(135, 150, 168)), 56, 52, 80, 5, 3, 3);

            var y = 98d;
            for (var paragraphIndex = 0; paragraphIndex < 8; paragraphIndex++)
            {
                graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(70, 70, 70)), 40, y, 160, 8, 3, 3);
                y += 18;

                for (var lineIndex = 0; lineIndex < 4; lineIndex++)
                {
                    var lineWidth = page.Width.Point - 120 - lineIndex * 8 - (paragraphIndex % 2 == 0 ? 0 : 24);
                    graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(145, 145, 145)), 40, y, lineWidth, 4, 2, 2);
                    y += 10;
                }

                y += 12;
            }
        }

        document.Save(path);
    }

    private void CreateVectorDiagramPdf(string path, string title, int pages)
    {
        DeleteFileIfExists(path);
        using var document = new PdfDocument();
        document.Info.Title = title;

        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(235, 245, 252)), 36, 28, 240, 24, 8, 8);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(20, 90, 140)), 50, 36, 140, 8, 3, 3);

            var pen = new XPen(XColor.FromArgb(0x18, 0x6f, 0xb5), 2.2);
            var accentPen = new XPen(XColor.FromArgb(0xd4, 0x46, 0x38), 1.6);
            var fill = new XSolidBrush(XColor.FromArgb(0xe8, 0xf3, 0xfb));
            var warningFill = new XSolidBrush(XColor.FromArgb(0xff, 0xf0, 0xd9));

            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    var x = 60 + column * 230;
                    var y = 100 + row * 150;
                    graphics.DrawRoundedRectangle(pen, fill, x, y, 150, 74, 12, 12);
                    graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(35, 35, 35)), x + 12, y + 12, 72, 6, 2, 2);
                    graphics.DrawEllipse(accentPen, warningFill, x + 102, y + 18, 30, 30);
                    graphics.DrawLine(pen, x + 150, y + 37, x + 210, y + 37);
                    graphics.DrawLine(pen, x + 75, y + 74, x + 75, y + 110);
                }
            }

            graphics.DrawRectangle(accentPen, 60, 560, 460, 120);
            for (var i = 0; i < 5; i++)
            {
                graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(120, 120, 120)), 72, 584 + i * 16, 360 - i * 22, 4, 2, 2);
            }
        }

        document.Save(path);
    }

    private void CreateMixedBrochurePdf(string path, string title, int pages)
    {
        DeleteFileIfExists(path);
        using var document = new PdfDocument();
        document.Info.Title = title;

        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var assetPath = CreateScanAsset($"mixed-{pageIndex + 1}", lowColor: false, width: 1200, height: 900, seed: 100 + pageIndex, useBmp: false);
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            using var image = XImage.FromFile(assetPath);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(235, 246, 251)), 36, 28, 280, 28, 8, 8);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(50, 50, 50)), 50, 36, 160, 8, 3, 3);
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(135, 145, 150)), 50, 50, 220, 4, 2, 2);
            graphics.DrawImage(image, 36, 100, 320, 240);
            graphics.DrawRectangle(new XPen(XColor.FromArgb(0x3a, 0x75, 0x9f), 1.4), 380, 100, 160, 240);

            var y = 120d;
            for (var bulletIndex = 0; bulletIndex < 8; bulletIndex++)
            {
                graphics.DrawEllipse(XBrushes.DarkSlateBlue, 392, y + 4, 5, 5);
                graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(75, 75, 75)), 404, y, 104 + bulletIndex % 3 * 10, 5, 2, 2);
                y += 24;
            }
        }

        document.Save(path);
    }

    private void CreateImageHeavyPdf(string path, string title, int pages, bool lowColor, bool useBmp)
    {
        DeleteFileIfExists(path);
        using var document = new PdfDocument();
        document.Info.Title = title;

        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            var assetPath = CreateScanAsset($"{Path.GetFileNameWithoutExtension(path)}-{pageIndex + 1}", lowColor, width: 1600, height: 2200, seed: 500 + pageIndex, useBmp: useBmp);
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            using var image = XImage.FromFile(assetPath);
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }

        document.Save(path);
    }

    private string CreateScanAsset(string key, bool lowColor, int width, int height, int seed, bool useBmp)
    {
        var extension = useBmp ? "bmp" : "jpg";
        var assetPath = Path.Combine(_assetsRoot, $"{key}.{extension}");
        if (File.Exists(assetPath))
        {
            return assetPath;
        }

        var random = new Random(seed);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var backgroundColor = lowColor ? Color.FromRgb(247, 247, 243) : Color.FromRgb(250, 249, 245);
            context.DrawRectangle(new SolidColorBrush(backgroundColor), null, new Rect(0, 0, width, height));

            for (var block = 0; block < 8; block++)
            {
                var x = random.Next(50, 220);
                var y = 120 + block * 240 + random.Next(-24, 24);
                var w = width - 2 * x;
                var h = random.Next(130, 210);
                var fill = lowColor
                    ? new SolidColorBrush(Color.FromRgb((byte)random.Next(220, 244), (byte)random.Next(220, 244), (byte)random.Next(220, 244)))
                    : new SolidColorBrush(Color.FromRgb((byte)random.Next(224, 250), (byte)random.Next(228, 246), (byte)random.Next(232, 248)));
                context.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromRgb(214, 214, 214)), 1), new Rect(x, y, w, h), 16, 16);

                for (var line = 0; line < 8; line++)
                {
                    var lineY = y + 24 + line * 18;
                    var lineWidth = w - random.Next(80, 220);
                    var stroke = lowColor
                        ? Color.FromRgb((byte)random.Next(70, 140), (byte)random.Next(70, 140), (byte)random.Next(70, 140))
                        : Color.FromRgb((byte)random.Next(40, 160), (byte)random.Next(45, 150), (byte)random.Next(50, 170));
                    context.DrawRectangle(new SolidColorBrush(stroke), null, new Rect(x + 18, lineY, lineWidth, random.Next(5, 8)));
                }
            }

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var labelBrush = lowColor ? Brushes.DimGray : new SolidColorBrush(Color.FromRgb(46, 64, 83));
            for (var stamp = 0; stamp < 6; stamp++)
            {
                var text = new FormattedText(
                    $"Scan sample {stamp + 1}",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    28,
                    labelBrush,
                    1.0);
                context.DrawText(text, new Point(90 + random.Next(-20, 30), 80 + stamp * 320 + random.Next(-25, 25)));
            }

            for (var accent = 0; accent < 180; accent++)
            {
                var x1 = random.Next(0, width);
                var y1 = random.Next(0, height);
                var x2 = Math.Clamp(x1 + random.Next(-18, 18), 0, width);
                var y2 = Math.Clamp(y1 + random.Next(-18, 18), 0, height);
                var alpha = (byte)random.Next(24, 65);
                var color = lowColor
                    ? Color.FromArgb(alpha, 115, 115, 115)
                    : Color.FromArgb(alpha, (byte)random.Next(70, 190), (byte)random.Next(70, 190), (byte)random.Next(70, 190));
                context.DrawLine(new Pen(new SolidColorBrush(color), random.NextDouble() * 1.4 + 0.6), new Point(x1, y1), new Point(x2, y2));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        var noisyBitmap = AddNoise(bitmap, lowColor, seed);

        BitmapEncoder encoder = useBmp
            ? new BmpBitmapEncoder()
            : new JpegBitmapEncoder { QualityLevel = 96 };
        encoder.Frames.Add(BitmapFrame.Create(noisyBitmap));
        using var stream = File.Create(assetPath);
        encoder.Save(stream);
        return assetPath;
    }

    private static BitmapSource AddNoise(BitmapSource source, bool lowColor, int seed)
    {
        var formatted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = formatted.PixelWidth * 4;
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);
        var random = new Random(seed * 17 + 29);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (lowColor)
            {
                var delta = random.Next(-14, 15);
                pixels[index] = ClampByte(pixels[index] + delta);
                pixels[index + 1] = ClampByte(pixels[index + 1] + delta);
                pixels[index + 2] = ClampByte(pixels[index + 2] + delta);
            }
            else
            {
                pixels[index] = ClampByte(pixels[index] + random.Next(-18, 19));
                pixels[index + 1] = ClampByte(pixels[index + 1] + random.Next(-18, 19));
                pixels[index + 2] = ClampByte(pixels[index + 2] + random.Next(-18, 19));
            }

            pixels[index + 3] = 255;
        }

        var bitmap = BitmapSource.Create(formatted.PixelWidth, formatted.PixelHeight, 144, 144, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
}
