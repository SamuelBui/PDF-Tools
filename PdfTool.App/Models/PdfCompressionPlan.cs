namespace PdfTool.App.Models;

public class PdfCompressionPlan
{
    public PdfCompressionStrategy Strategy { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public bool OptimizeImageObjects { get; set; }
    public bool AllowPageRasterization { get; set; }
    public bool RasterizeAllPages { get; set; }
    public int MixedPageTargetDpi { get; set; }
    public int ImageHeavyPageTargetDpi { get; set; }
    public int MixedPageJpegQuality { get; set; }
    public int ImageHeavyPageJpegQuality { get; set; }
    public bool PreferGrayscaleForLowColorPages { get; set; }
    public bool RequireScanLikePages { get; set; }
    public double MixedPageBudgetRatio { get; set; }
    public double ImageHeavyPageBudgetRatio { get; set; }
    public double MinimumSavingsRatio { get; set; }
    public int MaxImageDpi { get; set; }
    public int ImageJpegQuality { get; set; }
    public int MinimumImageJpegQuality { get; set; }
    public double TargetImageSavingsRatio { get; set; }
    public int MaxImagePixelDimension { get; set; }
    public bool AllowGrayscaleImageObjects { get; set; }
}
