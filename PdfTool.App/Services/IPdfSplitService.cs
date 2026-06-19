using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfSplitService
{
    PdfPageOrganizerDocumentInfo LoadDocumentInfo(string inputPath, string? password = null);
    OperationResult SplitEveryPage(string inputPath, string outputFolder);
    OperationResult SplitByRanges(string inputPath, string outputFolder, string pageRanges);
    OperationResult ExtractSelectedPages(PdfSplitOperationOptions options);
    OperationResult RemoveSelectedPages(PdfSplitOperationOptions options);
    OperationResult RotateSelectedPages(PdfSplitOperationOptions options);
}
