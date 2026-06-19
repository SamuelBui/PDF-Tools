namespace PdfTool.App.Models;

public class PdfDocumentInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? PageCount { get; set; }
    public bool IsEncrypted { get; set; }
    public bool HasOwnerPermissions { get; set; }
    public bool RequiresPassword { get; set; }
    public bool IsPasswordIncorrect { get; set; }
    public bool CanReadContents { get; set; }
    public bool Exists { get; set; }
    public bool IsPdf { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
