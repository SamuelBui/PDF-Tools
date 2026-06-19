using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfMergeService
{
    OperationResult Merge(IReadOnlyList<PdfFileItem> inputFiles, string outputPath);
}
