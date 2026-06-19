namespace PdfToolSmoke;

internal sealed class GeneratedCorpus
{
    public GeneratedCorpus(CorpusManifest manifest, IReadOnlyDictionary<string, CorpusFileDescriptor> byId)
    {
        Manifest = manifest;
        ById = byId;
    }

    public CorpusManifest Manifest { get; }
    public IReadOnlyDictionary<string, CorpusFileDescriptor> ById { get; }

    public CorpusFileDescriptor this[string id] => ById[id];
}

internal sealed class CorpusManifest
{
    public DateTime GeneratedAtLocal { get; set; }
    public string CorpusRoot { get; set; } = string.Empty;
    public List<CorpusFileDescriptor> Files { get; set; } = new();
}

internal sealed class CorpusFileDescriptor
{
    public string Id { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public int PageCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string? UserPassword { get; set; }
    public string? OwnerPassword { get; set; }
    public string AbsolutePath { get; set; } = string.Empty;
}

internal sealed class RegressionReport
{
    public DateTime ExecutedAtLocal { get; set; }
    public string CorpusManifestPath { get; set; } = string.Empty;
    public List<RegressionCaseResult> Results { get; set; } = new();
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
}

internal sealed class RegressionCaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
}
