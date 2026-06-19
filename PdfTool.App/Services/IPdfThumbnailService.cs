using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfThumbnailService
{
    IReadOnlyList<PdfPageThumbnailResult> RenderDocumentThumbnails(string filePath, int width, int height, int? maxPages = null, string? password = null);
}
