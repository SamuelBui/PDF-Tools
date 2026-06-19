namespace PdfTool.App.Models;

public class PdfCompressionValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long OutputSizeBytes { get; set; }
}
