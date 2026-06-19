using System.IO;
using System.Linq;
using System.Text.Json;
using PdfSharp.Pdf.IO;
using PdfTool.App.Services;

namespace PdfToolSmoke;

internal sealed partial class RegressionHarness
{
    private readonly string _projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    private readonly string _corpusRoot;
    private readonly string _inputRoot;
    private readonly string _derivedRoot;
    private readonly string _assetsRoot;
    private readonly string _resultsRoot;
    private readonly string _manifestPath;
    private readonly string _reportPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly IAppLogger _logger;
    private readonly IPdfProtectionService _protectionService;
    private readonly IPdfMergeService _mergeService;
    private readonly IPdfSplitService _splitService;
    private readonly IPdfCompressionService _compressionService;
    private readonly IPdfDocumentInspectorService _documentInspectorService;

    public RegressionHarness()
    {
        _corpusRoot = Path.Combine(_projectRoot, "RegressionCorpus");
        _inputRoot = Path.Combine(_corpusRoot, "Input");
        _derivedRoot = Path.Combine(_corpusRoot, "Derived");
        _assetsRoot = Path.Combine(_corpusRoot, "Assets");
        _resultsRoot = Path.Combine(_corpusRoot, "Results");
        _manifestPath = Path.Combine(_corpusRoot, "corpus-manifest.json");
        _reportPath = Path.Combine(_corpusRoot, "regression-report.json");

        _logger = new AppLogger();
        _documentInspectorService = new PdfDocumentInspectorService();
        var compressionInspector = new PdfCompressionInspectorService(_documentInspectorService);
        var imageOptimizationService = new PdfImageObjectOptimizationService();
        _protectionService = new PdfProtectionService(_logger);
        _mergeService = new PdfMergeService(_logger);
        _splitService = new PdfSplitService(_logger);
        _compressionService = new PdfCompressionService(compressionInspector, _documentInspectorService, imageOptimizationService, _logger);
    }

    public int Execute(HarnessMode mode)
    {
        try
        {
            var corpus = GenerateCorpus();
            Console.WriteLine($"Generated regression corpus at '{_corpusRoot}'.");

            if (mode == HarnessMode.GenerateOnly)
            {
                Console.WriteLine("Generation only mode complete.");
                return 0;
            }

            var report = RunRegression(corpus);
            File.WriteAllText(_reportPath, JsonSerializer.Serialize(report, _jsonOptions));

            Console.WriteLine();
            Console.WriteLine($"Regression complete. Passed: {report.PassedCount}, Failed: {report.FailedCount}");
            Console.WriteLine($"Manifest: {_manifestPath}");
            Console.WriteLine($"Report:   {_reportPath}");

            return report.FailedCount == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Regression harness failed: {ex}");
            return 2;
        }
    }

    public int CompressFile(string inputPath, string outputPath, string profile)
    {
        try
        {
            var normalizedProfile = profile.Trim().ToLowerInvariant();
            var compressionLevel = normalizedProfile switch
            {
                "safe" => 15,
                "balanced" or "balance" => 50,
                "strong" => 75,
                "extreme" => 95,
                _ => 50
            };

            var result = _compressionService.Compress(new PdfTool.App.Models.PdfCompressionOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                CompressionLevel = compressionLevel
            });

            Console.WriteLine(result.Message);
            if (result.CompressionRunSummary != null)
            {
                Console.WriteLine($"Method: {result.CompressionRunSummary.MethodUsed}");
                Console.WriteLine($"Optimized: {result.CompressionRunSummary.OptimizedImageCount}");
                Console.WriteLine($"Skipped: {result.CompressionRunSummary.SkippedImageCount}");
                Console.WriteLine($"Rasterized: {result.CompressionRunSummary.RasterizedPageCount}/{result.CompressionRunSummary.TotalPageCount}");
                Console.WriteLine($"Color: {result.CompressionRunSummary.ColorMode}");
            }

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 2;
        }
    }

    private RegressionReport RunRegression(GeneratedCorpus corpus)
    {
        Directory.CreateDirectory(_resultsRoot);

        var results = new List<RegressionCaseResult>
        {
            RunCase("protect-text-report", "Protect", () => TestProtectTextReport(corpus)),
            RunCase("protect-invalid-input", "Protect", () => TestProtectInvalidInput(corpus)),
            RunCase("unlock-requires-owner-password", "Unlock", () => TestUnlockRequiresOwnerPassword(corpus)),
            RunCase("unlock-owner-password", "Unlock", () => TestUnlockWithOwnerPassword(corpus)),
            RunCase("split-extract-selected", "Split", () => TestSplitExtractSelected(corpus)),
            RunCase("split-remove-selected", "Split", () => TestSplitRemoveSelected(corpus)),
            RunCase("split-rotate-selected", "Split", () => TestSplitRotateSelected(corpus)),
            RunCase("merge-basic", "Merge", () => TestMergeBasic(corpus)),
            RunCase("merge-reorder-and-rotate", "Merge", () => TestMergeReorderAndRotate(corpus)),
            RunCase("compress-color-safe", "Compress", () => TestCompressColorSafe(corpus)),
            RunCase("compress-color-balanced", "Compress", () => TestCompressColorBalanced(corpus)),
            RunCase("compress-unicode-path-balanced", "Compress", () => TestCompressUnicodePathBalanced(corpus)),
            RunCase("compress-lowcolor-strong", "Compress", () => TestCompressLowColorStrong(corpus)),
            RunCase("compress-color-extreme", "Compress", () => TestCompressColorExtreme(corpus))
        };

        return new RegressionReport
        {
            ExecutedAtLocal = DateTime.Now,
            CorpusManifestPath = _manifestPath,
            Results = results,
            PassedCount = results.Count(result => result.Passed),
            FailedCount = results.Count(result => !result.Passed)
        };
    }

    private RegressionCaseResult RunCase(string id, string area, Func<RegressionCaseResult> execute)
    {
        try
        {
            var result = execute();
            Console.WriteLine($"[{(result.Passed ? "PASS" : "FAIL")}] {area} :: {id} :: {result.Detail}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {area} :: {id} :: {ex.Message}");
            return new RegressionCaseResult
            {
                Id = id,
                Area = area,
                Passed = false,
                Detail = ex.Message
            };
        }
    }

    private CorpusFileDescriptor CreateDescriptor(string id, string absolutePath, string description, IReadOnlyList<string> tags, string? userPassword, string? ownerPassword)
    {
        var inspectionPassword = ownerPassword ?? userPassword;
        return new CorpusFileDescriptor
        {
            Id = id,
            RelativePath = GetRelativePath(absolutePath),
            AbsolutePath = absolutePath,
            Description = description,
            Tags = tags.ToArray(),
            PageCount = GetPageCount(absolutePath, inspectionPassword),
            FileSizeBytes = new FileInfo(absolutePath).Length,
            UserPassword = userPassword,
            OwnerPassword = ownerPassword
        };
    }

    private string GetRelativePath(string absolutePath) => Path.GetRelativePath(_corpusRoot, absolutePath);

    private static RegressionCaseResult Pass(string id, string area, string detail, string? outputPath)
        => new()
        {
            Id = id,
            Area = area,
            Passed = true,
            Detail = detail,
            OutputPath = outputPath
        };

    private static void ResetDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var directoryInfo = new DirectoryInfo(path);
        foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            file.IsReadOnly = false;
            file.Attributes = FileAttributes.Normal;
        }

        foreach (var file in directoryInfo.GetFiles())
        {
            file.Delete();
        }

        foreach (var directory in directoryInfo.GetDirectories())
        {
            directory.Delete(true);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var fileInfo = new FileInfo(path)
        {
            IsReadOnly = false,
            Attributes = FileAttributes.Normal
        };
        fileInfo.Delete();
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string actual, string expectedSubstring, string message)
    {
        if (actual?.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException($"{message} Actual message: '{actual}'.");
        }
    }

    private static int GetPageCount(string path, string? password = null)
    {
        using var document = string.IsNullOrWhiteSpace(password)
            ? PdfReader.Open(path, PdfDocumentOpenMode.Import)
            : PdfReader.Open(path, password, PdfDocumentOpenMode.Import, new PdfReaderOptions());
        return document.PageCount;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double length = bytes;

        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }
}
