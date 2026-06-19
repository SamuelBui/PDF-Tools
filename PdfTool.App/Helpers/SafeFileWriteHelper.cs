using System.IO;

namespace PdfTool.App.Helpers;

public static class SafeFileWriteHelper
{
    public static string CreateTemporaryOutputPath(string outputPath, string operationTag)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Output path is invalid.");
        Directory.CreateDirectory(directory);
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.{operationTag}.tmp{extension}");
    }

    public static void CommitTemporaryFile(string tempPath, string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath, true);
    }

    public static void TryDeleteTemporaryFile(string? tempPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
        }
    }
}
