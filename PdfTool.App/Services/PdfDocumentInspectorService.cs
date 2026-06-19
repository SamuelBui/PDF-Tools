using System.IO;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfDocumentInspectorService : IPdfDocumentInspectorService
{
    public PdfDocumentInfo Inspect(string filePath, string? password = null)
    {
        filePath ??= string.Empty;

        var info = new PdfDocumentInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Exists = File.Exists(filePath)
        };

        if (!info.Exists)
        {
            info.StatusMessage = "File does not exist.";
            return info;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            info.FileSizeBytes = fileInfo.Length;
            info.IsPdf = PdfReader.TestPdfFile(filePath) > 0;

            if (!info.IsPdf)
            {
                info.StatusMessage = "File is not a valid PDF.";
                return info;
            }

            using var document = string.IsNullOrWhiteSpace(password)
                ? PdfReader.Open(filePath, PdfDocumentOpenMode.Import)
                : PdfReader.Open(filePath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());
            info.PageCount = document.PageCount;
            info.IsEncrypted = document.SecuritySettings.IsEncrypted;
            info.HasOwnerPermissions = PdfSecurityAccessHelper.HasOwnerPermissions(document, password);
            info.CanReadContents = true;
            info.StatusMessage = info.IsEncrypted
                ? info.HasOwnerPermissions
                    ? "Encrypted PDF opened with owner permissions."
                    : "Encrypted PDF opened with user permissions only."
                : "Ready";
            return info;
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            info.IsEncrypted = message.Contains("password", StringComparison.OrdinalIgnoreCase)
                               || message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                               || message.Contains("decrypt", StringComparison.OrdinalIgnoreCase);
            info.RequiresPassword = info.IsEncrypted && string.IsNullOrWhiteSpace(password);
            info.IsPasswordIncorrect = info.IsEncrypted && !string.IsNullOrWhiteSpace(password);
            info.StatusMessage = info.RequiresPassword
                ? "Password required to open this PDF."
                : info.IsPasswordIncorrect
                    ? "Incorrect password for this PDF."
                    : message;
            return info;
        }
    }
}
