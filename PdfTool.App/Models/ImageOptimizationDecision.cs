namespace PdfTool.App.Models;

public enum ImageOptimizationDecision
{
    Skip,
    RecompressJpeg,
    DownsampleAndRecompress,
    ConvertToGrayscaleJpeg
}
