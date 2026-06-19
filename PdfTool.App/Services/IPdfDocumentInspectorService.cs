using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfDocumentInspectorService
{
    PdfDocumentInfo Inspect(string filePath, string? password = null);
}
