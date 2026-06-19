namespace PdfTool.App.Models;

public class PdfCompressionOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int CompressionLevel { get; set; } = 50;
    public string Password { get; set; } = string.Empty;
    public string OwnerPassword { get; set; } = string.Empty;

    public string GetEffectivePassword()
        => !string.IsNullOrWhiteSpace(OwnerPassword)
            ? OwnerPassword
            : Password;
}
