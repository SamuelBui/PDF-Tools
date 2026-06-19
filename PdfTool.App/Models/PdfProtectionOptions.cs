namespace PdfTool.App.Models;

public class PdfProtectionOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string UserPassword { get; set; } = string.Empty;
    public string OwnerPassword { get; set; } = string.Empty;
    public bool AllowPrint { get; set; } = true;
    public bool AllowFullQualityPrint { get; set; } = true;
    public bool AllowModifyDocument { get; set; } = true;
    public bool AllowExtractContent { get; set; } = true;
    public bool AllowAnnotations { get; set; } = true;
    public bool AllowFormsFill { get; set; } = true;
    public bool AllowAssembleDocument { get; set; } = true;
}
