namespace PdfTool.App.Models;

public class PdfCompressionPageAnalysis
{
    public int PageNumber { get; set; }
    public PdfCompressionPageCategory Category { get; set; }
    public double InkCoverage { get; set; }
    public double EdgeDensity { get; set; }
    public double AverageBrightness { get; set; }
    public double Colorfulness { get; set; }
    public bool PreferGrayscale { get; set; }
    public bool IsScanLike { get; set; }
}
