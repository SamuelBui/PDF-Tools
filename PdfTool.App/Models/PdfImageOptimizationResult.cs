namespace PdfTool.App.Models;

public class PdfImageOptimizationResult
{
    public bool Success { get; set; }
    public bool Optimized { get; set; }
    public ImageOptimizationDecision Decision { get; set; } = ImageOptimizationDecision.Skip;
    public string Reason { get; set; } = string.Empty;
    public long OriginalBytes { get; set; }
    public long NewBytes { get; set; }
}
