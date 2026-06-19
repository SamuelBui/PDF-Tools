using System.IO;
using PDFiumSharp;

namespace PdfTool.App.Helpers;

public sealed class PdfiumDocumentLease : IDisposable
{
    private readonly string? _temporaryPath;

    public PdfiumDocumentLease(PdfDocument document, string? temporaryPath)
    {
        Document = document;
        _temporaryPath = temporaryPath;
    }

    public PdfDocument Document { get; }

    public void Dispose()
    {
        Document.Dispose();

        try
        {
            if (!string.IsNullOrWhiteSpace(_temporaryPath) && File.Exists(_temporaryPath))
            {
                File.Delete(_temporaryPath);
            }
        }
        catch
        {
            // Temporary cleanup must not hide the original PDFium result.
        }
    }
}

public static class PdfiumFileHelper
{
    public static void ClearTemporaryCopies()
    {
        var tempFolder = GetTemporaryFolderPath();
        if (!Directory.Exists(tempFolder))
        {
            return;
        }

        foreach (var tempFile in Directory.EnumerateFiles(tempFolder, "*.pdf"))
        {
            TryDelete(tempFile);
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(tempFolder).Any())
            {
                Directory.Delete(tempFolder);
            }
        }
        catch
        {
            // Temporary cleanup is best-effort.
        }
    }

    public static PdfiumDocumentLease OpenDocument(string filePath, string? password = null)
    {
        if (RequiresAsciiProxy(filePath))
        {
            return OpenDocumentFromTemporaryCopy(filePath, password);
        }

        try
        {
            return new PdfiumDocumentLease(new PdfDocument(filePath, password ?? string.Empty), null);
        }
        catch (Exception ex) when (File.Exists(filePath) && ShouldRetryWithTemporaryCopy(ex))
        {
            return OpenDocumentFromTemporaryCopy(filePath, password);
        }
    }

    private static PdfiumDocumentLease OpenDocumentFromTemporaryCopy(string filePath, string? password)
    {
        var tempFolder = GetTemporaryFolderPath();
        Directory.CreateDirectory(tempFolder);

        var tempPath = Path.Combine(tempFolder, $"{Guid.NewGuid():N}.pdf");
        File.Copy(filePath, tempPath, overwrite: true);

        try
        {
            return new PdfiumDocumentLease(new PdfDocument(tempPath, password ?? string.Empty), tempPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static bool RequiresAsciiProxy(string filePath)
        => filePath.Any(character => character > 127);

    private static string GetTemporaryFolderPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfTool.App",
            "PdfiumTemp");

    private static bool ShouldRetryWithTemporaryCopy(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("PDFium Error", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
               || message.Contains("could not be opened", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
