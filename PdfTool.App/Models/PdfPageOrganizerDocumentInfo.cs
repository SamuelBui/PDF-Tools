namespace PdfTool.App.Models;

public class PdfPageOrganizerDocumentInfo
{
    public int PageCount { get; set; }
    public List<PdfPageOrganizerItem> Pages { get; set; } = new();
    public bool IsValidPdf { get; set; } = true;
    public bool IsEncrypted { get; set; }
    public bool HasOwnerPermissions { get; set; }
    public bool RequiresPassword { get; set; }
    public bool IsPasswordIncorrect { get; set; }
    public bool CanReadContents { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
