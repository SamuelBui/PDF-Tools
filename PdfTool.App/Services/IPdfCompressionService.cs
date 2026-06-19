using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfCompressionService
{
    OperationResult Compress(PdfCompressionOptions options);
}
