using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfCompressionInspectorService
{
    PdfCompressionInspectionResult Inspect(string filePath, string? password = null);
}
