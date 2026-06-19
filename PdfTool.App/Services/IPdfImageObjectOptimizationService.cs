using PdfSharp.Pdf;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfImageObjectOptimizationService
{
    PdfImageOptimizationSummary OptimizeImages(PdfDocument document, PdfCompressionPlan plan);
}
