using System.IO;
using PdfSharp.Pdf.IO;
using PdfTool.App.Models;

namespace PdfToolSmoke;

internal sealed partial class RegressionHarness
{
    private RegressionCaseResult TestProtectTextReport(GeneratedCorpus corpus)
    {
        var source = corpus["text-report"];
        var outputPath = Path.Combine(_resultsRoot, "protect-text-report.protected.pdf");
        var result = _protectionService.Protect(new PdfProtectionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            UserPassword = "Regression!123",
            OwnerPassword = "RegressionOwner!123",
            AllowPrint = false,
            AllowFullQualityPrint = false,
            AllowModifyDocument = false,
            AllowExtractContent = false,
            AllowAnnotations = false,
            AllowFormsFill = false,
            AllowAssembleDocument = false
        });

        AssertTrue(result.Success, result.Message);
        var withoutPassword = _documentInspectorService.Inspect(outputPath);
        AssertTrue(withoutPassword.RequiresPassword || withoutPassword.IsEncrypted, "Protected output should require a password.");

        var withUserPassword = _documentInspectorService.Inspect(outputPath, "Regression!123");
        AssertTrue(withUserPassword.IsPdf && withUserPassword.PageCount == source.PageCount, "Protected output should open with user password.");

        var withOwnerPassword = _documentInspectorService.Inspect(outputPath, "RegressionOwner!123");
        AssertTrue(withOwnerPassword.HasOwnerPermissions, "Owner password should grant owner permissions.");

        return Pass("protect-text-report", "Protect", $"Protected {source.PageCount} pages and enforced owner permissions.", outputPath);
    }

    private RegressionCaseResult TestProtectInvalidInput(GeneratedCorpus corpus)
    {
        var source = corpus["invalid-pseudo-pdf"];
        var outputPath = Path.Combine(_resultsRoot, "invalid-should-not-protect.pdf");
        var result = _protectionService.Protect(new PdfProtectionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            UserPassword = "Invalid!1234"
        });

        AssertTrue(!result.Success, "Protect should fail for a non-PDF input.");
        AssertContains(result.Message, "not a valid PDF", "Invalid input should report a PDF validation error.");
        return Pass("protect-invalid-input", "Protect", "Invalid pseudo-PDF rejected as expected.", null);
    }

    private RegressionCaseResult TestUnlockRequiresOwnerPassword(GeneratedCorpus corpus)
    {
        var source = corpus["protected-restricted"];
        var outputPath = Path.Combine(_resultsRoot, "unlock-user-only.pdf");
        var result = _protectionService.Unlock(new PdfUnlockOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            Password = source.UserPassword ?? string.Empty
        });

        AssertTrue(!result.Success, "Unlock should fail with user password only.");
        AssertContains(result.Message, "Owner password", "Unlock failure should explicitly require the owner password.");
        return Pass("unlock-requires-owner-password", "Unlock", "User password cannot remove restrictions.", null);
    }

    private RegressionCaseResult TestUnlockWithOwnerPassword(GeneratedCorpus corpus)
    {
        var source = corpus["protected-restricted"];
        var outputPath = Path.Combine(_resultsRoot, "unlock-owner-success.pdf");
        var result = _protectionService.Unlock(new PdfUnlockOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            Password = source.OwnerPassword ?? string.Empty
        });

        AssertTrue(result.Success, result.Message);
        var info = _documentInspectorService.Inspect(outputPath);
        AssertTrue(info.IsPdf && !info.RequiresPassword && !info.IsEncrypted, "Unlocked output should open without a password.");
        AssertTrue(info.PageCount == source.PageCount, "Unlocked output should preserve page count.");
        return Pass("unlock-owner-password", "Unlock", "Owner password removed protection successfully.", outputPath);
    }

    private RegressionCaseResult TestSplitExtractSelected(GeneratedCorpus corpus)
    {
        var source = corpus["mixed-brochure"];
        var outputFolder = Path.Combine(_resultsRoot, "split-extract");
        Directory.CreateDirectory(outputFolder);
        var result = _splitService.ExtractSelectedPages(new PdfSplitOperationOptions
        {
            InputPath = source.AbsolutePath,
            OutputFolder = outputFolder,
            SelectedPages = [1, 3],
            OutputStrategy = SplitOutputStrategy.SeparateFiles
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.OutputPaths.Count == 2, "Extract selected should produce two separate files.");
        foreach (var outputPath in result.OutputPaths)
        {
            AssertTrue(GetPageCount(outputPath) == 1, "Each extracted file should contain exactly one page.");
        }

        return Pass("split-extract-selected", "Split", "Extracted two selected pages into separate outputs.", outputFolder);
    }

    private RegressionCaseResult TestSplitRemoveSelected(GeneratedCorpus corpus)
    {
        var source = corpus["mixed-brochure"];
        var outputFolder = Path.Combine(_resultsRoot, "split-remove");
        Directory.CreateDirectory(outputFolder);
        var result = _splitService.RemoveSelectedPages(new PdfSplitOperationOptions
        {
            InputPath = source.AbsolutePath,
            OutputFolder = outputFolder,
            SelectedPages = [2],
            PageSequence = [1, 2, 3]
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.OutputPath is not null, "Remove selected should return a single output file.");
        AssertTrue(GetPageCount(result.OutputPath!) == 2, "Removing one page from a three-page file should leave two pages.");
        return Pass("split-remove-selected", "Split", "Removed one selected page and preserved the remaining pages.", result.OutputPath);
    }

    private RegressionCaseResult TestSplitRotateSelected(GeneratedCorpus corpus)
    {
        var source = corpus["vector-diagrams"];
        var outputFolder = Path.Combine(_resultsRoot, "split-rotate");
        Directory.CreateDirectory(outputFolder);
        var result = _splitService.RotateSelectedPages(new PdfSplitOperationOptions
        {
            InputPath = source.AbsolutePath,
            OutputFolder = outputFolder,
            SelectedPages = [1],
            RotationDelta = 90
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.OutputPath is not null, "Rotate selected should return a single output file.");
        using var document = PdfReader.Open(result.OutputPath!, PdfDocumentOpenMode.Import);
        AssertTrue(document.PageCount == source.PageCount, "Rotate selected should preserve page count.");
        AssertTrue((document.Pages[0].Rotate % 360 + 360) % 360 == 90, "First page should be rotated 90 degrees.");
        return Pass("split-rotate-selected", "Split", "Rotated the selected page and preserved document structure.", result.OutputPath);
    }

    private RegressionCaseResult TestMergeBasic(GeneratedCorpus corpus)
    {
        var outputPath = Path.Combine(_resultsRoot, "merge-basic.pdf");
        var result = _mergeService.Merge(
        [
            new PdfFileItem { FilePath = corpus["text-report"].AbsolutePath, PageCount = corpus["text-report"].PageCount },
            new PdfFileItem { FilePath = corpus["vector-diagrams"].AbsolutePath, PageCount = corpus["vector-diagrams"].PageCount },
            new PdfFileItem { FilePath = corpus["mixed-brochure"].AbsolutePath, PageCount = corpus["mixed-brochure"].PageCount }
        ], outputPath);

        AssertTrue(result.Success, result.Message);
        AssertTrue(GetPageCount(outputPath) == corpus["text-report"].PageCount + corpus["vector-diagrams"].PageCount + corpus["mixed-brochure"].PageCount,
            "Merged output should contain the combined page count.");
        return Pass("merge-basic", "Merge", "Merged three source PDFs into a single output.", outputPath);
    }

    private RegressionCaseResult TestMergeReorderAndRotate(GeneratedCorpus corpus)
    {
        var fileA = new PdfFileItem
        {
            FilePath = corpus["text-report"].AbsolutePath,
            PageCount = corpus["text-report"].PageCount
        };
        fileA.PageThumbnails.Add(new PdfPageOrganizerItem
        {
            PageNumber = 1,
            SourcePageNumber = 3,
            SourceFilePath = fileA.FilePath
        });
        fileA.PageThumbnails.Add(new PdfPageOrganizerItem
        {
            PageNumber = 2,
            SourcePageNumber = 1,
            SourceFilePath = fileA.FilePath
        });

        var fileB = new PdfFileItem
        {
            FilePath = corpus["vector-diagrams"].AbsolutePath,
            PageCount = corpus["vector-diagrams"].PageCount
        };
        fileB.PageThumbnails.Add(new PdfPageOrganizerItem
        {
            PageNumber = 1,
            SourcePageNumber = 2,
            SourceFilePath = fileB.FilePath,
            Rotation = 90
        });

        var outputPath = Path.Combine(_resultsRoot, "merge-reorder-rotate.pdf");
        var result = _mergeService.Merge([fileA, fileB], outputPath);

        AssertTrue(result.Success, result.Message);
        using var document = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
        AssertTrue(document.PageCount == 3, "Reordered merge should contain exactly three pages.");
        AssertTrue((document.Pages[2].Rotate % 360 + 360) % 360 == 90, "Rotated page should keep its 90-degree rotation in the merged output.");
        return Pass("merge-reorder-and-rotate", "Merge", "Merged reordered pages and preserved page rotation.", outputPath);
    }

    private RegressionCaseResult TestCompressColorSafe(GeneratedCorpus corpus)
    {
        var source = corpus["scan-color"];
        var outputPath = Path.Combine(_resultsRoot, "scan-color-safe.compressed.pdf");
        var result = _compressionService.Compress(new PdfCompressionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            CompressionLevel = 15
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.CompressionRunSummary is not null, "Compression should return a run summary.");
        AssertTrue(GetPageCount(outputPath) == source.PageCount, "Compression should preserve page count.");
        AssertTrue(new FileInfo(outputPath).Length < source.FileSizeBytes, "Safe compression should reduce file size for an image-heavy color scan.");
        AssertTrue(result.CompressionRunSummary!.RasterizedPageCount == 0, "Safe compression must not rasterize pages.");
        AssertTrue(result.CompressionRunSummary.TextVectorPreserved, "Safe compression should preserve text/vector structure.");
        AssertTrue(!result.CompressionRunSummary!.ColorMode.Contains("Gray", StringComparison.OrdinalIgnoreCase),
            "Safe compression should preserve color mode.");
        return Pass("compress-color-safe", "Compress", $"{FormatBytes(source.FileSizeBytes)} -> {FormatBytes(new FileInfo(outputPath).Length)}.", outputPath);
    }

    private RegressionCaseResult TestCompressColorBalanced(GeneratedCorpus corpus)
    {
        var source = corpus["scan-color"];
        var outputPath = Path.Combine(_resultsRoot, "scan-color-balanced.compressed.pdf");
        var result = _compressionService.Compress(new PdfCompressionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            CompressionLevel = 50
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.CompressionRunSummary is not null, "Compression should return a run summary.");
        AssertTrue(GetPageCount(outputPath) == source.PageCount, "Compression should preserve page count.");
        AssertTrue(new FileInfo(outputPath).Length < source.FileSizeBytes, "Balanced compression should reduce file size.");
        AssertTrue(result.CompressionRunSummary!.RasterizedPageCount == 0, "Balanced compression must not rasterize pages.");
        AssertTrue(result.CompressionRunSummary.TextVectorPreserved, "Balanced compression should preserve text/vector structure.");
        AssertTrue(!result.CompressionRunSummary!.ColorMode.Contains("Gray", StringComparison.OrdinalIgnoreCase),
            "Balanced compression should remain in color according to the current product rule.");
        return Pass("compress-color-balanced", "Compress", $"{FormatBytes(source.FileSizeBytes)} -> {FormatBytes(new FileInfo(outputPath).Length)}.", outputPath);
    }

    private RegressionCaseResult TestCompressLowColorStrong(GeneratedCorpus corpus)
    {
        var source = corpus["scan-lowcolor"];
        var outputPath = Path.Combine(_resultsRoot, "scan-lowcolor-strong.compressed.pdf");
        var result = _compressionService.Compress(new PdfCompressionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            CompressionLevel = 75
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.CompressionRunSummary is not null, "Compression should return a run summary.");
        AssertTrue(GetPageCount(outputPath) == source.PageCount, "Compression should preserve page count.");
        AssertTrue(new FileInfo(outputPath).Length < source.FileSizeBytes, "Strong compression should reduce file size for a low-color scan.");
        AssertTrue(result.CompressionRunSummary!.MethodUsed.Contains("Strong", StringComparison.OrdinalIgnoreCase),
            "Strong compression should report the Strong profile.");
        return Pass("compress-lowcolor-strong", "Compress", $"{FormatBytes(source.FileSizeBytes)} -> {FormatBytes(new FileInfo(outputPath).Length)}.", outputPath);
    }

    private RegressionCaseResult TestCompressUnicodePathBalanced(GeneratedCorpus corpus)
    {
        var source = corpus["scan-color"];
        var unicodeInputPath = Path.Combine(_inputRoot, "04-ảnh kiểm tra tiếng Việt.pdf");
        File.Copy(source.AbsolutePath, unicodeInputPath, overwrite: true);

        var outputPath = Path.Combine(_resultsRoot, "unicode-path-balanced.compressed.pdf");
        var result = _compressionService.Compress(new PdfCompressionOptions
        {
            InputPath = unicodeInputPath,
            OutputPath = outputPath,
            CompressionLevel = 50
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.CompressionRunSummary is not null, "Compression should return a run summary.");
        AssertTrue(GetPageCount(outputPath) == source.PageCount, "Compression should preserve page count for Unicode input paths.");
        AssertTrue(new FileInfo(outputPath).Length < new FileInfo(unicodeInputPath).Length, "Balanced compression should reduce file size for Unicode input paths.");
        return Pass("compress-unicode-path-balanced", "Compress", $"{FormatBytes(new FileInfo(unicodeInputPath).Length)} -> {FormatBytes(new FileInfo(outputPath).Length)}.", outputPath);
    }

    private RegressionCaseResult TestCompressColorExtreme(GeneratedCorpus corpus)
    {
        var source = corpus["scan-color"];
        var outputPath = Path.Combine(_resultsRoot, "scan-color-extreme.compressed.pdf");
        var result = _compressionService.Compress(new PdfCompressionOptions
        {
            InputPath = source.AbsolutePath,
            OutputPath = outputPath,
            CompressionLevel = 95
        });

        AssertTrue(result.Success, result.Message);
        AssertTrue(result.CompressionRunSummary is not null, "Compression should return a run summary.");
        AssertTrue(GetPageCount(outputPath) == source.PageCount, "Extreme compression should preserve page count.");
        AssertTrue(new FileInfo(outputPath).Length < source.FileSizeBytes, "Extreme compression should reduce file size.");
        AssertTrue(result.CompressionRunSummary!.RasterizedPageCount == source.PageCount, "Extreme compression should rasterize every page.");
        AssertTrue(result.CompressionRunSummary.MethodUsed.Contains("Extreme", StringComparison.OrdinalIgnoreCase),
            "Extreme compression should report the Extreme profile.");
        return Pass("compress-color-extreme", "Compress", $"{FormatBytes(source.FileSizeBytes)} -> {FormatBytes(new FileInfo(outputPath).Length)}.", outputPath);
    }
}
