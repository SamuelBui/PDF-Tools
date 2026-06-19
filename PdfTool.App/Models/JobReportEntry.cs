namespace PdfTool.App.Models;

public class JobReportEntry
{
    public DateTime TimestampLocal { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
