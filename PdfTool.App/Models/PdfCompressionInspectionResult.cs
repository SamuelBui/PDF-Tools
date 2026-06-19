namespace PdfTool.App.Models;

public class PdfCompressionInspectionResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
    public bool Exists { get; set; }
    public bool IsPdf { get; set; }
    public bool IsEncrypted { get; set; }
    public bool HasOwnerPermissions { get; set; }
    public bool RequiresPassword { get; set; }
    public bool IsPasswordIncorrect { get; set; }
    public bool CanReadContents { get; set; }
    public int TextVectorPageCount { get; set; }
    public int MixedPageCount { get; set; }
    public int ImageHeavyPageCount { get; set; }
    public PdfCompressionStrategy SuggestedStrategy { get; set; }
    public string Guidance { get; set; } = string.Empty;
    public IReadOnlyList<string> RiskWarnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PdfCompressionPageAnalysis> Pages { get; set; } = Array.Empty<PdfCompressionPageAnalysis>();
}
