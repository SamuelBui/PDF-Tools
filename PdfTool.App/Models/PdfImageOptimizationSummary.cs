namespace PdfTool.App.Models;

public class PdfImageOptimizationSummary
{
    public int CandidateImageCount { get; set; }
    public int OptimizedImageCount { get; set; }
    public int SkippedImageCount { get; set; }
    public long OriginalImageBytes { get; set; }
    public long OptimizedImageBytes { get; set; }
    public List<PdfImageOptimizationResult> Results { get; set; } = new();
}
