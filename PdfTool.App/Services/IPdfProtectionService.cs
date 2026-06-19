using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IPdfProtectionService
{
    OperationResult Protect(PdfProtectionOptions options);
    OperationResult Unlock(PdfUnlockOptions options);
}
