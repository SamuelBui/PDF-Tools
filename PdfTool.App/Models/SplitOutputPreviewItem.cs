namespace PdfTool.App.Models;

public class SplitOutputPreviewItem
{
    public string FileName { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string FileSizeText { get; set; } = string.Empty;
}
