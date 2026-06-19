namespace PdfTool.App.Models;

public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public List<string> OutputPaths { get; set; } = new();
    public PdfCompressionRunSummary? CompressionRunSummary { get; set; }

    public static OperationResult Ok(string message, string? outputPath = null)
        => new() { Success = true, Message = message, OutputPath = outputPath };

    public static OperationResult Fail(string message)
        => new() { Success = false, Message = message };
}
