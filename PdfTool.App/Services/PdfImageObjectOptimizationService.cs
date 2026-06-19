using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfImageObjectOptimizationService : IPdfImageObjectOptimizationService
{
    private const int MinimumImageDimension = 256;

    public PdfImageOptimizationSummary OptimizeImages(PdfDocument document, PdfCompressionPlan plan)
    {
        var summary = new PdfImageOptimizationSummary();
        var processedObjects = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in document.Pages)
        {
            var resources = page.Elements.GetDictionary("/Resources");
            OptimizeImagesInResources(resources, plan, processedObjects, summary);
        }

        return summary;
    }

    private static void OptimizeImagesInResources(
        PdfDictionary? resources,
        PdfCompressionPlan plan,
        HashSet<string> processedObjects,
        PdfImageOptimizationSummary summary)
    {
        var xObjects = resources?.Elements.GetDictionary("/XObject");
        if (xObjects == null)
        {
            return;
        }

        foreach (var element in xObjects.Elements)
        {
            var dictionary = ResolveDictionary(element.Value, out var objectKey);
            if (dictionary == null || !processedObjects.Add(objectKey))
            {
                continue;
            }

            if (!TryGetNameElement(dictionary, "/Subtype", out var subtype))
            {
                continue;
            }

            if (subtype == "/Image")
            {
                summary.CandidateImageCount++;
                var result = TryOptimizeImageXObject(dictionary, plan);
                summary.Results.Add(result);

                if (result.Optimized)
                {
                    summary.OptimizedImageCount++;
                    summary.OriginalImageBytes += result.OriginalBytes;
                    summary.OptimizedImageBytes += result.NewBytes;
                }
                else
                {
                    summary.SkippedImageCount++;
                }
            }
            else if (subtype == "/Form")
            {
                var nestedResources = dictionary.Elements.GetDictionary("/Resources");
                OptimizeImagesInResources(nestedResources, plan, processedObjects, summary);
            }
        }
    }

    private static PdfDictionary? ResolveDictionary(PdfItem? item, out string objectKey)
    {
        if (item is PdfReference reference && reference.Value is PdfDictionary referencedDictionary)
        {
            objectKey = reference.ObjectID.ToString();
            return referencedDictionary;
        }

        if (item is PdfDictionary dictionary)
        {
            objectKey = $"direct:{dictionary.GetHashCode()}";
            return dictionary;
        }

        objectKey = string.Empty;
        return null;
    }

    private static PdfImageOptimizationResult TryOptimizeImageXObject(PdfDictionary imageDictionary, PdfCompressionPlan plan)
    {
        var result = new PdfImageOptimizationResult
        {
            Success = false,
            Decision = ImageOptimizationDecision.Skip
        };

        var stream = imageDictionary.Stream;
        if (stream == null)
        {
            result.Reason = "Image has no stream.";
            return result;
        }

        var originalBytes = stream.Value;
        result.OriginalBytes = originalBytes?.LongLength ?? 0;
        if (originalBytes == null || originalBytes.Length == 0)
        {
            result.Reason = "Image stream is empty.";
            return result;
        }

        if (!TryGetOptimizableImageInfo(imageDictionary, out var imageInfo, out var reason))
        {
            result.Reason = reason;
            return result;
        }

        var width = imageDictionary.Elements.GetInteger("/Width");
        var height = imageDictionary.Elements.GetInteger("/Height");
        if (width < MinimumImageDimension || height < MinimumImageDimension)
        {
            result.Reason = "Image is too small to optimize safely.";
            return result;
        }

        try
        {
            var effectivePlan = imageInfo.ColorSpace.IsCmyk
                ? CreateCmykColorSafePlan(plan)
                : plan;
            var bitmap = DecodeImage(imageDictionary, originalBytes, imageInfo);
            var targetBitmap = DownsampleIfNeeded(bitmap, effectivePlan.MaxImagePixelDimension);
            var decision = ReferenceEquals(bitmap, targetBitmap)
                ? ImageOptimizationDecision.RecompressJpeg
                : ImageOptimizationDecision.DownsampleAndRecompress;

            if (imageInfo.ColorSpace.ComponentCount == 1)
            {
                targetBitmap = ConvertBitmapFormat(targetBitmap, PixelFormats.Gray8);
            }
            else if (imageInfo.ColorSpace.IsCmyk)
            {
                targetBitmap = ConvertBitmapFormat(targetBitmap, PixelFormats.Cmyk32);
            }
            else
            {
                targetBitmap = ConvertBitmapFormat(targetBitmap, PixelFormats.Rgb24);
            }

            var optimized = EncodeJpegAdaptive(targetBitmap, effectivePlan, originalBytes.Length);
            var optimizedBytes = optimized.Bytes;
            result.NewBytes = optimizedBytes.Length;
            result.Decision = decision;
            result.Success = true;

            var savingsRatio = 1d - (optimizedBytes.Length / (double)Math.Max(1, originalBytes.Length));
            if (optimizedBytes.Length >= originalBytes.Length || savingsRatio < plan.MinimumSavingsRatio)
            {
                result.Reason = "Optimized image was not meaningfully smaller.";
                return result;
            }

            stream.Value = optimizedBytes;
            imageDictionary.Elements.SetInteger("/Width", targetBitmap.PixelWidth);
            imageDictionary.Elements.SetInteger("/Height", targetBitmap.PixelHeight);
            imageDictionary.Elements.SetInteger("/BitsPerComponent", 8);
            imageDictionary.Elements.SetName("/Filter", "/DCTDecode");
            if (!imageInfo.ColorSpace.PreserveOriginalItem)
            {
                imageDictionary.Elements.SetName("/ColorSpace", imageInfo.ColorSpace.Name);
            }

            imageDictionary.Elements.Remove("/DecodeParms");
            imageDictionary.Elements.SetInteger("/Length", optimizedBytes.Length);

            result.Optimized = true;
            result.Reason = imageInfo.ColorSpace.IsCmyk
                ? $"CMYK image recompressed with color-safe settings at JPEG quality {optimized.Quality}."
                : $"{imageInfo.ColorSpace.DisplayName} image recompressed at JPEG quality {optimized.Quality}.";
            return result;
        }
        catch (Exception ex)
        {
            result.Reason = $"Image optimization failed: {ex.Message}";
            return result;
        }
    }

    private static bool TryGetOptimizableImageInfo(
        PdfDictionary imageDictionary,
        out OptimizableImageInfo imageInfo,
        out string reason)
    {
        imageInfo = OptimizableImageInfo.Empty;
        reason = string.Empty;

        if (!TryGetSupportedImageEncoding(imageDictionary, out var encoding))
        {
            reason = "Only simple DCT/JPEG or Flate RGB/gray images are supported.";
            return false;
        }

        if (!TryGetSupportedColorSpace(imageDictionary, out var colorSpace, out reason))
        {
            return false;
        }

        if (colorSpace.IsCmyk && imageDictionary.Elements.ContainsKey("/Decode"))
        {
            reason = "CMYK images with custom Decode arrays are preserved to avoid channel inversion.";
            return false;
        }

        if (imageDictionary.Elements.GetBoolean("/ImageMask"))
        {
            reason = "Image masks are skipped.";
            return false;
        }

        if (imageDictionary.Elements.ContainsKey("/SMask") || imageDictionary.Elements.ContainsKey("/Mask"))
        {
            reason = "Images with masks or transparency are skipped.";
            return false;
        }

        if (imageDictionary.Elements.ContainsKey("/DecodeParms"))
        {
            reason = "Images with decode parameters are skipped.";
            return false;
        }

        if (encoding == ImageEncoding.Flate && colorSpace.IsCmyk)
        {
            reason = "Flate CMYK images are preserved to avoid color profile shifts.";
            return false;
        }

        if (encoding == ImageEncoding.Flate && imageDictionary.Elements.ContainsKey("/Decode"))
        {
            reason = "Flate images with custom Decode arrays are preserved.";
            return false;
        }

        if (imageDictionary.Elements.GetInteger("/BitsPerComponent") != 8)
        {
            reason = "Only 8-bit images are supported.";
            return false;
        }

        imageInfo = new OptimizableImageInfo(encoding, colorSpace);
        return true;
    }

    private static bool TryGetSupportedColorSpace(
        PdfDictionary dictionary,
        out ImageColorSpaceInfo colorSpace,
        out string reason)
    {
        colorSpace = ImageColorSpaceInfo.Empty;
        reason = string.Empty;

        if (!dictionary.Elements.ContainsKey("/ColorSpace"))
        {
            reason = "Images without an explicit color space are skipped.";
            return false;
        }

        var item = ResolveReference(dictionary.Elements["/ColorSpace"]);
        if (TryResolveName(item, out var name))
        {
            if (name == "/DeviceRGB")
            {
                colorSpace = new ImageColorSpaceInfo(name, "RGB", 3, false, false);
                return true;
            }

            if (name == "/DeviceGray")
            {
                colorSpace = new ImageColorSpaceInfo(name, "grayscale", 1, false, false);
                return true;
            }

            if (name == "/DeviceCMYK")
            {
                colorSpace = new ImageColorSpaceInfo(name, "CMYK", 4, true, false);
                return true;
            }

            reason = $"Unsupported image color space {name}.";
            return false;
        }

        if (item is PdfArray array && array.Elements.Count > 0)
        {
            var first = ResolveReference(array.Elements[0]);
            if (TryResolveName(first, out var familyName))
            {
                if (familyName == "/ICCBased")
                {
                    if (TryGetIccComponentCount(array, out var componentCount))
                    {
                        if (componentCount == 3)
                        {
                            colorSpace = new ImageColorSpaceInfo("/ICCBased", "ICCBased RGB", 3, false, true);
                            return true;
                        }

                        if (componentCount == 1)
                        {
                            colorSpace = new ImageColorSpaceInfo("/ICCBased", "ICCBased grayscale", 1, false, true);
                            return true;
                        }

                        reason = "ICCBased CMYK images are preserved to avoid color profile shifts.";
                        return false;
                    }

                    reason = "ICCBased images without a readable component count are preserved.";
                    return false;
                }

                reason = $"Unsupported image color space family {familyName}.";
                return false;
            }
        }

        reason = "Unsupported complex image color space.";
        return false;
    }

    private static bool TryGetSupportedImageEncoding(PdfDictionary dictionary, out ImageEncoding encoding)
    {
        encoding = ImageEncoding.Unsupported;

        if (!dictionary.Elements.ContainsKey("/Filter"))
        {
            return false;
        }

        var item = ResolveReference(dictionary.Elements["/Filter"]);
        if (TryResolveName(item, out var filterName))
        {
            return TryMapEncoding(filterName, out encoding);
        }

        if (item is PdfArray array && array.Elements.Count == 1)
        {
            return TryResolveName(ResolveReference(array.Elements[0]), out filterName)
                   && TryMapEncoding(filterName, out encoding);
        }

        return false;
    }

    private static bool TryMapEncoding(string filterName, out ImageEncoding encoding)
    {
        if (filterName == "/DCTDecode")
        {
            encoding = ImageEncoding.Dct;
            return true;
        }

        if (filterName == "/FlateDecode")
        {
            encoding = ImageEncoding.Flate;
            return true;
        }

        encoding = ImageEncoding.Unsupported;
        return false;
    }

    private static bool TryGetIccComponentCount(PdfArray colorSpaceArray, out int componentCount)
    {
        componentCount = 0;
        if (colorSpaceArray.Elements.Count < 2)
        {
            return false;
        }

        var profile = ResolveReference(colorSpaceArray.Elements[1]) as PdfDictionary;
        if (profile == null || !profile.Elements.ContainsKey("/N"))
        {
            return false;
        }

        componentCount = profile.Elements.GetInteger("/N");
        return componentCount is 1 or 3 or 4;
    }

    private static PdfCompressionPlan CreateCmykColorSafePlan(PdfCompressionPlan plan)
        => new()
        {
            ImageJpegQuality = Math.Max(plan.ImageJpegQuality, 85),
            MinimumImageJpegQuality = Math.Max(plan.MinimumImageJpegQuality, 85),
            TargetImageSavingsRatio = Math.Min(plan.TargetImageSavingsRatio, 0.50),
            MinimumSavingsRatio = Math.Min(plan.MinimumSavingsRatio, 0.04),
            MaxImagePixelDimension = plan.MaxImagePixelDimension <= 0
                ? 3200
                : Math.Max(plan.MaxImagePixelDimension, 3200)
        };

    private static bool TryGetNameElement(PdfDictionary dictionary, string key, out string name)
    {
        name = string.Empty;

        if (!dictionary.Elements.ContainsKey(key))
        {
            return false;
        }

        return TryResolveName(ResolveReference(dictionary.Elements[key]), out name);
    }

    private static PdfItem? ResolveReference(PdfItem? item)
        => item is PdfReference reference ? reference.Value : item;

    private static bool TryResolveName(PdfItem? item, out string name)
    {
        name = string.Empty;

        if (item is PdfName pdfName)
        {
            name = pdfName.Value;
            return true;
        }

        return false;
    }

    private static BitmapSource DecodeImage(
        PdfDictionary imageDictionary,
        byte[] encodedBytes,
        OptimizableImageInfo imageInfo)
    {
        return imageInfo.Encoding == ImageEncoding.Flate
            ? DecodeFlateImage(imageDictionary, imageInfo.ColorSpace)
            : DecodeJpeg(encodedBytes);
    }

    private static BitmapSource DecodeJpeg(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapSource DecodeFlateImage(PdfDictionary imageDictionary, ImageColorSpaceInfo colorSpace)
    {
        var stream = imageDictionary.Stream ?? throw new InvalidOperationException("Image has no stream.");
        var width = imageDictionary.Elements.GetInteger("/Width");
        var height = imageDictionary.Elements.GetInteger("/Height");
        var pixelFormat = colorSpace.ComponentCount == 1
            ? PixelFormats.Gray8
            : PixelFormats.Rgb24;
        var stride = checked((width * pixelFormat.BitsPerPixel + 7) / 8);
        var expectedBytes = checked(stride * height);
        var unfilteredBytes = stream.UnfilteredValue;

        if (unfilteredBytes.Length < expectedBytes)
        {
            throw new InvalidOperationException("Flate image stream is shorter than expected.");
        }

        if (unfilteredBytes.Length != expectedBytes)
        {
            var trimmedBytes = new byte[expectedBytes];
            Array.Copy(unfilteredBytes, trimmedBytes, expectedBytes);
            unfilteredBytes = trimmedBytes;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            pixelFormat,
            null,
            unfilteredBytes,
            stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource ConvertBitmapFormat(BitmapSource source, PixelFormat targetFormat)
    {
        if (source.Format == targetFormat)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, targetFormat, null, 0);
        converted.Freeze();
        return converted;
    }

    private static BitmapSource DownsampleIfNeeded(BitmapSource bitmap, int maxPixelDimension)
    {
        if (maxPixelDimension <= 0)
        {
            return bitmap;
        }

        var largestDimension = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
        if (largestDimension <= maxPixelDimension)
        {
            return bitmap;
        }

        var scale = maxPixelDimension / (double)largestDimension;
        var transformed = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        transformed.Freeze();
        return transformed;
    }

    private static byte[] EncodeJpeg(BitmapSource bitmap, int quality)
    {
        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = Math.Clamp(quality, 1, 100)
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static JpegOptimizationCandidate EncodeJpegAdaptive(BitmapSource bitmap, PdfCompressionPlan plan, long originalByteCount)
    {
        var startQuality = Math.Clamp(plan.ImageJpegQuality, 1, 100);
        var minimumQuality = plan.MinimumImageJpegQuality > 0
            ? Math.Clamp(plan.MinimumImageJpegQuality, 1, startQuality)
            : startQuality;
        var targetSavingsRatio = Math.Clamp(plan.TargetImageSavingsRatio, 0, 0.95);

        JpegOptimizationCandidate? bestCandidate = null;
        for (var quality = startQuality; quality >= minimumQuality; quality -= 2)
        {
            var bytes = EncodeJpeg(bitmap, quality);
            var candidate = new JpegOptimizationCandidate(bytes, quality);
            bestCandidate ??= candidate;

            if (bytes.Length < bestCandidate.Bytes.Length)
            {
                bestCandidate = candidate;
            }

            var savingsRatio = 1d - (bytes.Length / (double)Math.Max(1, originalByteCount));
            if (savingsRatio >= targetSavingsRatio)
            {
                return candidate;
            }
        }

        return bestCandidate ?? new JpegOptimizationCandidate(EncodeJpeg(bitmap, startQuality), startQuality);
    }

    private sealed record JpegOptimizationCandidate(byte[] Bytes, int Quality);

    private sealed record OptimizableImageInfo(ImageEncoding Encoding, ImageColorSpaceInfo ColorSpace)
    {
        public static OptimizableImageInfo Empty { get; } = new(ImageEncoding.Unsupported, ImageColorSpaceInfo.Empty);
    }

    private sealed record ImageColorSpaceInfo(
        string Name,
        string DisplayName,
        int ComponentCount,
        bool IsCmyk,
        bool PreserveOriginalItem)
    {
        public static ImageColorSpaceInfo Empty { get; } = new(string.Empty, string.Empty, 0, false, false);
    }

    private enum ImageEncoding
    {
        Unsupported,
        Dct,
        Flate
    }
}
