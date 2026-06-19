using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFiumSharp;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfThumbnailService : IPdfThumbnailService
{
    private static readonly object RenderSync = new();
    private readonly IAppLogger _logger;

    public PdfThumbnailService(IAppLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PdfPageThumbnailResult> RenderDocumentThumbnails(string filePath, int width, int height, int? maxPages = null, string? password = null)
    {
        var results = new List<PdfPageThumbnailResult>();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || width <= 0 || height <= 0)
        {
            return results;
        }

        lock (RenderSync)
        {
            try
            {
                var pdfiumPath = Path.Combine(AppContext.BaseDirectory, "pdfium.dll");
                if (!File.Exists(pdfiumPath))
                {
                    _logger.LogWarning($"Thumbnail rendering is unavailable because pdfium.dll was not found next to the app. Base directory: {AppContext.BaseDirectory}");
                    return results;
                }

                using var documentLease = PdfiumFileHelper.OpenDocument(filePath, password);
                var document = documentLease.Document;
                var pageCount = maxPages.HasValue
                    ? Math.Min(document.Pages.Count, maxPages.Value)
                    : document.Pages.Count;

                for (var index = 0; index < pageCount; index++)
                {
                    using var page = document.Pages[index];
                    var image = RenderingExtensionsWpf.CreateImageSource(
                        page,
                        width,
                        height,
                        true,
                        page.Orientation,
                        RenderingFlags.None);

                    if (image is Freezable freezable && freezable.CanFreeze)
                    {
                        freezable.Freeze();
                    }

                    results.Add(new PdfPageThumbnailResult
                    {
                        PageNumber = index + 1,
                        Thumbnail = image
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Thumbnail rendering failed for '{filePath}'.", ex);
                return results;
            }
        }

        return results;
    }
}
