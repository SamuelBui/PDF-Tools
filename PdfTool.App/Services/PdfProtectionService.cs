using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfProtectionService : IPdfProtectionService
{
    private readonly IAppLogger _logger;

    public PdfProtectionService(IAppLogger logger)
    {
        _logger = logger;
    }

    public OperationResult Protect(PdfProtectionOptions options)
    {
        string? tempOutputPath = null;

        try
        {
            _logger.LogInfo($"Protect start. Input='{options.InputPath}', Output='{options.OutputPath}'.");

            if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFile(options.OutputPath, out var outputError))
            {
                return OperationResult.Fail(outputError);
            }

            if (PdfReader.TestPdfFile(options.InputPath) <= 0)
            {
                return OperationResult.Fail("Input file is not a valid PDF.");
            }

            if (string.IsNullOrWhiteSpace(options.UserPassword))
            {
                return OperationResult.Fail("User password cannot be empty.");
            }

            if (!PasswordHelper.MeetsStrongPolicy(options.UserPassword))
            {
                return OperationResult.Fail("User password must be at least 12 characters and include uppercase, lowercase, number, and special character.");
            }

            if (string.Equals(options.InputPath, options.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Output PDF must be different from input PDF.");
            }

            var ownerPassword = string.IsNullOrWhiteSpace(options.OwnerPassword)
                ? null
                : options.OwnerPassword;

            if (!string.IsNullOrWhiteSpace(ownerPassword) && !PasswordHelper.MeetsStrongPolicy(ownerPassword))
            {
                return OperationResult.Fail("Owner password must be at least 12 characters and include uppercase, lowercase, number, and special character.");
            }

            var restrictionsApplied =
                !options.AllowPrint ||
                !options.AllowFullQualityPrint ||
                !options.AllowModifyDocument ||
                !options.AllowExtractContent ||
                !options.AllowAnnotations ||
                !options.AllowFormsFill ||
                !options.AllowAssembleDocument;

            if (restrictionsApplied && string.IsNullOrWhiteSpace(ownerPassword))
            {
                return OperationResult.Fail("Owner password is required when permissions are restricted.");
            }

            using var document = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Modify);
            var security = document.SecuritySettings;
            security.UserPassword = options.UserPassword;

            if (!string.IsNullOrWhiteSpace(ownerPassword))
            {
                security.OwnerPassword = ownerPassword;
            }

            security.PermitPrint = options.AllowPrint;
            security.PermitFullQualityPrint = options.AllowFullQualityPrint;
            security.PermitModifyDocument = options.AllowModifyDocument;
            security.PermitExtractContent = options.AllowExtractContent;
            security.PermitAnnotations = options.AllowAnnotations;
            security.PermitFormsFill = options.AllowFormsFill;
            security.PermitAssembleDocument = options.AllowAssembleDocument;

            tempOutputPath = SafeFileWriteHelper.CreateTemporaryOutputPath(options.OutputPath, "protect");
            document.Save(tempOutputPath);
            SafeFileWriteHelper.CommitTemporaryFile(tempOutputPath, options.OutputPath);
            tempOutputPath = null;

            _logger.LogInfo($"Protect success. Output='{options.OutputPath}'.");

            return OperationResult.Ok("PDF protected successfully.", options.OutputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Protect failed. Input='{options.InputPath}', Output='{options.OutputPath}'.", ex);
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Input PDF is already protected or requires a password. Unlock it before applying new protection.");
            }

            return OperationResult.Fail($"Protect failed: {ex.Message}");
        }
        finally
        {
            SafeFileWriteHelper.TryDeleteTemporaryFile(tempOutputPath);
        }
    }

    public OperationResult Unlock(PdfUnlockOptions options)
    {
        string? tempOutputPath = null;

        try
        {
            _logger.LogInfo($"Unlock start. Input='{options.InputPath}', Output='{options.OutputPath}'.");

            if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFile(options.OutputPath, out var outputError))
            {
                return OperationResult.Fail(outputError);
            }

            if (PdfReader.TestPdfFile(options.InputPath) <= 0)
            {
                return OperationResult.Fail("Input file is not a valid PDF.");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                return OperationResult.Fail("Password cannot be empty.");
            }

            if (string.Equals(options.InputPath, options.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Output PDF must be different from input PDF.");
            }

            try
            {
                using var probe = PdfReader.Open(options.InputPath, PdfDocumentOpenMode.Import);
                if (!probe.SecuritySettings.IsEncrypted)
                {
                    return OperationResult.Fail("Input PDF is not protected.");
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
            }

            if (!PdfSecurityAccessHelper.TryHasOwnerLevelAccess(options.InputPath, options.Password, out _))
            {
                return OperationResult.Fail("Owner password is required to remove PDF protection and permission restrictions.");
            }

            using var source = PdfReader.Open(options.InputPath, options.Password, PdfDocumentOpenMode.Import, new PdfReaderOptions());

            using var target = new PdfDocument();

            target.Info.Title = source.Info.Title;
            target.Info.Author = source.Info.Author;
            target.Info.Subject = source.Info.Subject;
            target.Info.Keywords = source.Info.Keywords;
            target.Info.Creator = source.Info.Creator;

            for (var index = 0; index < source.PageCount; index++)
            {
                target.AddPage(source.Pages[index]);
            }

            tempOutputPath = SafeFileWriteHelper.CreateTemporaryOutputPath(options.OutputPath, "unlock");
            target.Save(tempOutputPath);
            SafeFileWriteHelper.CommitTemporaryFile(tempOutputPath, options.OutputPath);
            tempOutputPath = null;
            _logger.LogInfo($"Unlock success. Output='{options.OutputPath}'.");
            return OperationResult.Ok("PDF unlocked successfully.", options.OutputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unlock failed. Input='{options.InputPath}', Output='{options.OutputPath}'.", ex);
            if (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Incorrect password or the PDF could not be unlocked with the supplied password.");
            }

            return OperationResult.Fail($"Unlock failed: {ex.Message}");
        }
        finally
        {
            SafeFileWriteHelper.TryDeleteTemporaryFile(tempOutputPath);
        }
    }
}
