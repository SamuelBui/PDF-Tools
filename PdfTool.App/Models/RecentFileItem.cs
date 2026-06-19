using System.IO;

namespace PdfTool.App.Models;

public class RecentFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastUsedUtc { get; set; }
    public string FileName => Path.GetFileName(FilePath);
    public string FolderPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(FolderPath) ? FileName : $"{FileName}   ({FolderPath})";
}
