using System.IO;

namespace PdfTool.App.Helpers;

public static class FileAccessHelper
{
    public static bool TryValidateReadableFile(string inputPath, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Please choose an input PDF file.";
            return false;
        }

        if (!File.Exists(inputPath))
        {
            errorMessage = "Input PDF does not exist.";
            return false;
        }

        try
        {
            using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.None);
            errorMessage = string.Empty;
            return true;
        }
        catch (IOException)
        {
            errorMessage = "Input PDF is currently open or locked by another process. Close the file, then try again.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Input PDF cannot be accessed.";
            return false;
        }
    }

    public static bool TryValidateOutputFile(string outputPath, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errorMessage = "Please choose an output PDF path.";
            return false;
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            errorMessage = "Output PDF path is invalid.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);

            if (File.Exists(outputPath))
            {
                using var existingFile = new FileStream(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                var tempPath = Path.Combine(directoryPath, $"{Guid.NewGuid():N}.tmp");
                using (var tempFile = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                }

                File.Delete(tempPath);
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (IOException)
        {
            errorMessage = "Output file or folder is currently locked.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Output PDF path cannot be written.";
            return false;
        }
    }

    public static bool TryValidateOutputFolder(string outputFolder, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            errorMessage = "Please choose an output folder.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(outputFolder);
            var tempPath = Path.Combine(outputFolder, $"{Guid.NewGuid():N}.tmp");

            using (var tempFile = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
            }

            File.Delete(tempPath);
            errorMessage = string.Empty;
            return true;
        }
        catch (IOException)
        {
            errorMessage = "Output folder is currently locked.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Output folder cannot be written.";
            return false;
        }
    }
}
