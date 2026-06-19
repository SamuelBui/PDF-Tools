using System.Windows.Media;

namespace PdfTool.App.Models;

public class PdfPageThumbnailResult
{
    public int PageNumber { get; set; }
    public ImageSource? Thumbnail { get; set; }
    public string PageLabel => $"Page {PageNumber:000}";
}
