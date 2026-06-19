using System.IO;

namespace PdfTool.App.Helpers;

public static class FileNameHelper
{
    public static string CreateProtectedFilePath(string inputPath)
    {
        var folder = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(folder, $"{name}.protected.pdf");
    }

    public static string CreateUnlockedFilePath(string inputPath)
    {
        var folder = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(folder, $"{name}.unlocked.pdf");
    }

    public static string CreateMergedFilePath(string? firstInputPath)
    {
        if (string.IsNullOrWhiteSpace(firstInputPath))
        {
            return "merged.pdf";
        }

        var folder = Path.GetDirectoryName(firstInputPath) ?? string.Empty;
        return Path.Combine(folder, "merged.pdf");
    }

    public static string CreateSplitFolderPath(string inputPath)
    {
        var folder = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(folder, $"{name}_split");
    }

    public static string CreateCompressedFilePath(string inputPath)
    {
        var folder = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(folder, $"{name}.compressed.pdf");
    }
}
