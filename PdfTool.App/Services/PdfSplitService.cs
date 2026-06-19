using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfSplitService : IPdfSplitService
{
    private readonly IAppLogger _logger;

    public PdfSplitService(IAppLogger logger)
    {
        _logger = logger;
    }

    public PdfPageOrganizerDocumentInfo LoadDocumentInfo(string inputPath, string? password = null)
    {
        var result = new PdfPageOrganizerDocumentInfo();

        if (!FileAccessHelper.TryValidateReadableFile(inputPath, out _))
        {
            result.StatusMessage = "Input PDF is currently open or locked by another process. Close the file, then try again.";
            return result;
        }

        try
        {
            if (PdfReader.TestPdfFile(inputPath) <= 0)
            {
                result.IsValidPdf = false;
                result.StatusMessage = "Input file is not a valid PDF.";
                return result;
            }

            using var source = string.IsNullOrWhiteSpace(password)
                ? PdfReader.Open(inputPath, PdfDocumentOpenMode.Import)
                : PdfReader.Open(inputPath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());
            result.PageCount = source.PageCount;
            result.IsEncrypted = source.SecuritySettings.IsEncrypted;
            result.HasOwnerPermissions = PdfSecurityAccessHelper.HasOwnerPermissions(source, password);
            result.CanReadContents = true;
            result.StatusMessage = "Ready";

            for (var index = 0; index < source.PageCount; index++)
            {
                var page = source.Pages[index];
                result.Pages.Add(new PdfPageOrganizerItem
                {
                    PageNumber = index + 1,
                    SourcePageNumber = index + 1,
                    SourceFilePath = inputPath,
                    WidthPoints = page.Width.Point,
                    HeightPoints = page.Height.Point,
                    Rotation = page.Rotate
                });
            }
        }
        catch (Exception ex)
        {
            var isPasswordRelated = ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("decrypt", StringComparison.OrdinalIgnoreCase);
            result.IsEncrypted = isPasswordRelated;
            result.RequiresPassword = isPasswordRelated;
            result.IsPasswordIncorrect = isPasswordRelated && !string.IsNullOrWhiteSpace(password);
            result.StatusMessage = isPasswordRelated
                ? string.IsNullOrWhiteSpace(password)
                    ? "Input PDF is protected and requires a password before organizing pages."
                    : "Incorrect password for this protected PDF."
                : $"Unable to read PDF pages: {ex.Message}";
        }

        return result;
    }

    public OperationResult SplitEveryPage(string inputPath, string outputFolder)
        => ExtractSelectedPages(new PdfSplitOperationOptions
        {
            InputPath = inputPath,
            OutputFolder = outputFolder,
            SelectedPages = LoadDocumentInfo(inputPath).Pages.Select(page => page.PageNumber).ToList(),
            OutputStrategy = SplitOutputStrategy.SeparateFiles
        });

    public OperationResult SplitByRanges(string inputPath, string outputFolder, string pageRanges)
    {
        try
        {
            _logger.LogInfo($"Split by ranges start. Input='{inputPath}', OutputFolder='{outputFolder}', Ranges='{pageRanges}'.");
            if (!FileAccessHelper.TryValidateReadableFile(inputPath, out var inputError))
            {
                return OperationResult.Fail(inputError);
            }

            if (!FileAccessHelper.TryValidateOutputFolder(outputFolder, out var folderError))
            {
                return OperationResult.Fail(folderError);
            }

            Directory.CreateDirectory(outputFolder);

            using var source = OpenInputDocument(inputPath, null);
            var parsedRanges = PageRangeParser.Parse(pageRanges, source.PageCount);
            var outputs = new List<string>();
            var baseName = Path.GetFileNameWithoutExtension(inputPath);

            foreach (var range in parsedRanges)
            {
                using var target = new PdfDocument();
                foreach (var pageNumber in range)
                {
                    target.AddPage(source.Pages[pageNumber - 1]);
                }

                var outputPath = Path.Combine(outputFolder, CreateRangeFileName(baseName, range));
                SaveDocumentSafely(target, outputPath, "split-range");
                outputs.Add(outputPath);
            }

            _logger.LogInfo($"Split by ranges success. Input='{inputPath}', FilesCreated={outputs.Count}.");
            return new OperationResult
            {
                Success = true,
                Message = $"Split by ranges completed. {outputs.Count} file(s) created.",
                OutputPaths = outputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Split by ranges failed. Input='{inputPath}', OutputFolder='{outputFolder}'.", ex);
            return OperationResult.Fail($"Split by ranges failed: {ex.Message}");
        }
    }

    public OperationResult ExtractSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            _logger.LogInfo($"Extract selected start. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}'.");
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            var selectedPages = options.SelectedPages
                .Distinct()
                .ToList();

            using var source = OpenInputDocument(options.InputPath, options.Password);
            if (!TryValidatePageConfiguration(source, options, requireSelection: true, out validationError))
            {
                return OperationResult.Fail(validationError);
            }

            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var outputs = new List<string>();

            switch (options.OutputStrategy)
            {
                case SplitOutputStrategy.SeparateFiles:
                    foreach (var pageNumber in selectedPages)
                    {
                        using var target = new PdfDocument();
                        var page = target.AddPage(source.Pages[pageNumber - 1]);
                        ApplyConfiguredRotation(page, pageNumber, options);

                        var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_page_{pageNumber:000}.pdf");
                        SaveDocumentSafely(target, outputPath, "extract-page");
                        outputs.Add(outputPath);
                    }

                    break;

                case SplitOutputStrategy.RangeFiles:
                    foreach (var range in BuildContiguousRanges(selectedPages))
                    {
                        using var target = new PdfDocument();
                        foreach (var pageNumber in range)
                        {
                            var page = target.AddPage(source.Pages[pageNumber - 1]);
                            ApplyConfiguredRotation(page, pageNumber, options);
                        }

                        var outputPath = Path.Combine(options.OutputFolder, CreateRangeFileName(baseName, range));
                        SaveDocumentSafely(target, outputPath, "extract-range");
                        outputs.Add(outputPath);
                    }

                    break;

                default:
                    using (var target = new PdfDocument())
                    {
                        foreach (var pageNumber in selectedPages)
                        {
                            var page = target.AddPage(source.Pages[pageNumber - 1]);
                            ApplyConfiguredRotation(page, pageNumber, options);
                        }

                        var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_selected.pdf");
                        SaveDocumentSafely(target, outputPath, "extract-selection");
                        outputs.Add(outputPath);
                    }

                    break;
            }

            return new OperationResult
            {
                Success = true,
                Message = $"Extract completed. {outputs.Count} file(s) created.",
                OutputPaths = outputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Extract selected failed. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}'.", ex);
            return OperationResult.Fail($"Extract failed: {ex.Message}");
        }
    }

    public OperationResult RemoveSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            _logger.LogInfo($"Remove selected start. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}'.");
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            using var source = OpenInputDocument(options.InputPath, options.Password);
            if (!TryValidatePageConfiguration(source, options, requireSelection: true, out validationError))
            {
                return OperationResult.Fail(validationError);
            }

            var selectedPages = options.SelectedPages.Distinct().ToHashSet();
            var pageSequence = options.PageSequence.Count > 0
                ? options.PageSequence
                : Enumerable.Range(1, source.PageCount).ToList();
            var remainingPages = pageSequence
                .Where(page => !selectedPages.Contains(page))
                .ToList();

            if (remainingPages.Count == 0)
            {
                return OperationResult.Fail("Cannot remove all pages. At least one page must remain.");
            }

            using var target = new PdfDocument();
            foreach (var pageNumber in remainingPages)
            {
                var page = target.AddPage(source.Pages[pageNumber - 1]);
                ApplyConfiguredRotation(page, pageNumber, options);
            }

            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_without_selected.pdf");
            SaveDocumentSafely(target, outputPath, "remove-selected");

            return new OperationResult
            {
                Success = true,
                Message = $"Remove selected pages completed. {remainingPages.Count} page(s) remain.",
                OutputPath = outputPath,
                OutputPaths = new List<string> { outputPath }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Remove selected failed. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}'.", ex);
            return OperationResult.Fail($"Remove selected pages failed: {ex.Message}");
        }
    }

    public OperationResult RotateSelectedPages(PdfSplitOperationOptions options)
    {
        try
        {
            _logger.LogInfo($"Rotate selected start. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}', Delta={options.RotationDelta}.");
            if (!TryValidateOptions(options, requireSelection: true, out var validationError))
            {
                return OperationResult.Fail(validationError);
            }

            Directory.CreateDirectory(options.OutputFolder);

            using var source = OpenInputDocument(options.InputPath, options.Password);
            if (!TryValidatePageConfiguration(source, options, requireSelection: true, out validationError))
            {
                return OperationResult.Fail(validationError);
            }

            using var target = new PdfDocument();
            var selectedPages = options.SelectedPages.Distinct().ToHashSet();

            for (var index = 0; index < source.PageCount; index++)
            {
                var page = target.AddPage(source.Pages[index]);
                var pageNumber = index + 1;

                if (selectedPages.Contains(pageNumber))
                {
                    var rotated = (page.Rotate + options.RotationDelta) % 360;
                    page.Rotate = rotated < 0 ? rotated + 360 : rotated;
                }
            }

            var baseName = Path.GetFileNameWithoutExtension(options.InputPath);
            var suffix = options.RotationDelta >= 0 ? "rotated_right" : "rotated_left";
            var outputPath = Path.Combine(options.OutputFolder, $"{baseName}_{suffix}.pdf");
            SaveDocumentSafely(target, outputPath, "rotate-pages");

            return new OperationResult
            {
                Success = true,
                Message = "Rotate selected pages completed.",
                OutputPath = outputPath,
                OutputPaths = new List<string> { outputPath }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Rotate selected failed. Input='{options.InputPath}', OutputFolder='{options.OutputFolder}'.", ex);
            return OperationResult.Fail($"Rotate selected pages failed: {ex.Message}");
        }
    }

    private static void SaveDocumentSafely(PdfDocument document, string outputPath, string operationTag)
    {
        string? tempOutputPath = null;

        try
        {
            tempOutputPath = SafeFileWriteHelper.CreateTemporaryOutputPath(outputPath, operationTag);
            document.Save(tempOutputPath);
            SafeFileWriteHelper.CommitTemporaryFile(tempOutputPath, outputPath);
            tempOutputPath = null;
        }
        finally
        {
            SafeFileWriteHelper.TryDeleteTemporaryFile(tempOutputPath);
        }
    }

    private static PdfSharp.Pdf.PdfDocument OpenInputDocument(string inputPath, string? password)
        => string.IsNullOrWhiteSpace(password)
            ? PdfReader.Open(inputPath, PdfDocumentOpenMode.Import)
            : PdfReader.Open(inputPath, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());

    private static void ApplyConfiguredRotation(PdfPage page, int pageNumber, PdfSplitOperationOptions options)
    {
        if (options.PageRotations.TryGetValue(pageNumber, out var rotation))
        {
            page.Rotate = rotation;
        }
    }

    private static bool TryValidateOptions(PdfSplitOperationOptions options, bool requireSelection, out string validationError)
    {
        if (!FileAccessHelper.TryValidateReadableFile(options.InputPath, out var inputError))
        {
            validationError = inputError;
            return false;
        }

        if (!FileAccessHelper.TryValidateOutputFolder(options.OutputFolder, out var outputError))
        {
            validationError = outputError;
            return false;
        }

        if (requireSelection && (options.SelectedPages == null || options.SelectedPages.Count == 0))
        {
            validationError = "Please select at least one page.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static bool TryValidatePageConfiguration(PdfDocument source, PdfSplitOperationOptions options, bool requireSelection, out string validationError)
    {
        var invalidSelectedPages = options.SelectedPages
            .Where(page => page < 1 || page > source.PageCount)
            .Distinct()
            .OrderBy(page => page)
            .ToList();

        if (requireSelection && invalidSelectedPages.Count > 0)
        {
            validationError = $"Selected page(s) out of range: {string.Join(", ", invalidSelectedPages)}.";
            return false;
        }

        if (options.PageSequence.Count > 0)
        {
            var invalidSequencePages = options.PageSequence
                .Where(page => page < 1 || page > source.PageCount)
                .Distinct()
                .OrderBy(page => page)
                .ToList();

            if (invalidSequencePages.Count > 0)
            {
                validationError = $"Page order contains out-of-range page(s): {string.Join(", ", invalidSequencePages)}.";
                return false;
            }

            if (options.PageSequence.Distinct().Count() != options.PageSequence.Count)
            {
                validationError = "Page order contains duplicate entries.";
                return false;
            }
        }

        validationError = string.Empty;
        return true;
    }

    private static List<List<int>> BuildContiguousRanges(IReadOnlyList<int> sortedPages)
    {
        var ranges = new List<List<int>>();
        if (sortedPages.Count == 0)
        {
            return ranges;
        }

        var currentRange = new List<int> { sortedPages[0] };
        for (var i = 1; i < sortedPages.Count; i++)
        {
            if (sortedPages[i] == sortedPages[i - 1] + 1)
            {
                currentRange.Add(sortedPages[i]);
                continue;
            }

            ranges.Add(currentRange);
            currentRange = new List<int> { sortedPages[i] };
        }

        ranges.Add(currentRange);
        return ranges;
    }

    private static string CreateRangeFileName(string baseName, IReadOnlyList<int> range)
    {
        if (range.Count == 1)
        {
            return $"{baseName}_p{range[0]}.pdf";
        }

        return $"{baseName}_p{range.First()}-p{range.Last()}.pdf";
    }
}
