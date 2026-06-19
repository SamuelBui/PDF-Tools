namespace PdfTool.App.Models;

public class PdfSplitOperationOptions
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public IReadOnlyList<int> SelectedPages { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> PageSequence { get; set; } = Array.Empty<int>();
    public IReadOnlyDictionary<int, int> PageRotations { get; set; } = new Dictionary<int, int>();
    public SplitOutputStrategy OutputStrategy { get; set; }
    public int RotationDelta { get; set; }
}
