using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.App.Helpers;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class PdfMergeService : IPdfMergeService
{
    private readonly IAppLogger _logger;

    public PdfMergeService(IAppLogger logger)
    {
        _logger = logger;
    }

    public OperationResult Merge(IReadOnlyList<PdfFileItem> inputFiles, string outputPath)
    {
        string? tempOutputPath = null;

        try
        {
            _logger.LogInfo($"Merge start. Output='{outputPath}', Files={inputFiles.Count}.");
            var totalPages = inputFiles.Sum(file => file.PageThumbnails.Count > 0
                ? file.PageThumbnails.Count
                : file.PageCount.GetValueOrDefault());

            if (totalPages <= 0)
            {
                return OperationResult.Fail("Please select at least one page to merge.");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return OperationResult.Fail("Please choose an output PDF path.");
            }

            if (!FileAccessHelper.TryValidateOutputFile(outputPath, out var outputError))
            {
                return OperationResult.Fail(outputError);
            }

            using var outputDocument = new PdfDocument();
            var sourceDocuments = new Dictionary<string, PdfSharp.Pdf.PdfDocument>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var inputFile in inputFiles)
                {
                    var pageSequence = inputFile.PageThumbnails.Count > 0
                        ? inputFile.PageThumbnails.ToList()
                        : BuildDefaultPageSequence(inputFile);

                    foreach (var sourcePage in pageSequence)
                    {
                        var sourcePath = string.IsNullOrWhiteSpace(sourcePage.SourceFilePath)
                            ? inputFile.FilePath
                            : sourcePage.SourceFilePath;
                        var sourcePassword = sourcePage.SourcePassword;

                        if (!FileAccessHelper.TryValidateReadableFile(sourcePath, out var inputError))
                        {
                            return OperationResult.Fail($"{sourcePath}: {inputError}");
                        }

                        var documentKey = CreateDocumentKey(sourcePath, sourcePassword);
                        if (!sourceDocuments.TryGetValue(documentKey, out var sourceDocument))
                        {
                            sourceDocument = string.IsNullOrWhiteSpace(sourcePassword)
                                ? PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import)
                                : PdfReader.Open(sourcePath, sourcePassword, PdfDocumentOpenMode.Import, new PdfReaderOptions());
                            sourceDocuments[documentKey] = sourceDocument;
                        }

                        var sourcePageNumber = sourcePage.SourcePageNumber > 0 ? sourcePage.SourcePageNumber : sourcePage.PageNumber;
                        var page = outputDocument.AddPage(sourceDocument.Pages[sourcePageNumber - 1]);
                        if (sourcePage.Rotation != 0)
                        {
                            var rotated = (page.Rotate + sourcePage.Rotation) % 360;
                            page.Rotate = rotated < 0 ? rotated + 360 : rotated;
                        }
                    }
                }
            }
            finally
            {
                foreach (var sourceDocument in sourceDocuments.Values)
                {
                    sourceDocument.Dispose();
                }
            }

            tempOutputPath = SafeFileWriteHelper.CreateTemporaryOutputPath(outputPath, "merge");
            outputDocument.Save(tempOutputPath);
            SafeFileWriteHelper.CommitTemporaryFile(tempOutputPath, outputPath);
            tempOutputPath = null;
            _logger.LogInfo($"Merge success. Output='{outputPath}', TotalPages={totalPages}.");
            return OperationResult.Ok("Merge completed successfully.", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Merge failed. Output='{outputPath}'.", ex);
            return OperationResult.Fail($"Merge failed: {ex.Message}");
        }
        finally
        {
            SafeFileWriteHelper.TryDeleteTemporaryFile(tempOutputPath);
        }
    }

    private static List<PdfPageOrganizerItem> BuildDefaultPageSequence(PdfFileItem inputFile)
    {
        var pageCount = inputFile.PageCount.GetValueOrDefault();
        if (pageCount <= 0)
        {
            return new List<PdfPageOrganizerItem>();
        }

        return Enumerable.Range(1, pageCount)
            .Select(pageNumber => new PdfPageOrganizerItem
            {
                PageNumber = pageNumber,
                SourcePageNumber = pageNumber,
                SourceFilePath = inputFile.FilePath,
                SourcePassword = inputFile.Password
            })
            .ToList();
    }

    private static string CreateDocumentKey(string sourcePath, string sourcePassword)
        => $"{sourcePath}|{sourcePassword}";
}
