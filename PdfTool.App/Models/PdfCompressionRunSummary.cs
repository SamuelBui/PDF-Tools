namespace PdfTool.App.Models;

public class PdfCompressionRunSummary
{
    public string MethodUsed { get; set; } = string.Empty;
    public int TotalPageCount { get; set; }
    public int OptimizedImageCount { get; set; }
    public int SkippedImageCount { get; set; }
    public int RasterizedPageCount { get; set; }
    public int GrayscalePageCount { get; set; }
    public bool TextVectorPreserved { get; set; }
    public bool LinksPreserved { get; set; }
    public string ColorMode { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
}
