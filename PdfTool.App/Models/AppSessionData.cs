using System.Windows;

namespace PdfTool.App.Models;

public class AppSessionData
{
    public int Version { get; set; } = 1;
    public int SelectedTabIndex { get; set; }
    public WindowSessionState Window { get; set; } = new();
    public ProtectSessionState Protect { get; set; } = new();
    public SplitSessionState Split { get; set; } = new();
    public MergeSessionState Merge { get; set; } = new();
    public CompressSessionState Compress { get; set; } = new();
}

public class WindowSessionState
{
    public double Left { get; set; } = 120;
    public double Top { get; set; } = 120;
    public double Width { get; set; } = 1320;
    public double Height { get; set; } = 860;
    public WindowState WindowState { get; set; } = WindowState.Normal;
}

public class ProtectSessionState
{
    public ProtectActionMode ActionMode { get; set; } = ProtectActionMode.Protect;
    public ProtectInputMode InputMode { get; set; } = ProtectInputMode.SingleFile;
    public BatchPasswordStrategy BatchPasswordStrategy { get; set; } = BatchPasswordStrategy.OnePasswordForAll;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string BatchSourceFolder { get; set; } = string.Empty;
    public string BatchOutputFolder { get; set; } = string.Empty;
    public bool AllowPrint { get; set; } = true;
    public bool AllowFullQualityPrint { get; set; } = true;
    public bool AllowModifyDocument { get; set; } = true;
    public bool AllowExtractContent { get; set; } = true;
    public bool AllowAnnotations { get; set; } = true;
    public bool AllowFormsFill { get; set; } = true;
    public bool AllowAssembleDocument { get; set; } = true;
    public int SelectedBatchItemIndex { get; set; } = -1;
    public List<ProtectBatchItemSessionState> BatchItems { get; set; } = new();
}

public class ProtectBatchItemSessionState
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public class SplitSessionState
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public SplitSelectionPreset SelectionPreset { get; set; } = SplitSelectionPreset.EveryPage;
    public SplitOutputStrategy OutputStrategy { get; set; } = SplitOutputStrategy.SeparateFiles;
    public string PageSelectionInput { get; set; } = string.Empty;
    public List<SplitPageSessionState> Pages { get; set; } = new();
}

public class SplitPageSessionState
{
    public int SourcePageNumber { get; set; }
    public int Rotation { get; set; }
    public bool IsSelected { get; set; }
}

public class MergeSessionState
{
    public string OutputPath { get; set; } = string.Empty;
    public string LastOutputPath { get; set; } = string.Empty;
    public int SelectedFileIndex { get; set; } = -1;
    public List<MergeFileSessionState> Files { get; set; } = new();
}

public class MergeFileSessionState
{
    public string FilePath { get; set; } = string.Empty;
    public int? PageCount { get; set; }
    public bool IsEncrypted { get; set; }
    public bool RequiresPassword { get; set; }
    public bool IsPasswordIncorrect { get; set; }
    public bool IsLocked { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsValidPdf { get; set; } = true;
    public string ValidationMessage { get; set; } = string.Empty;
    public List<MergePageSessionState> Pages { get; set; } = new();
}

public class MergePageSessionState
{
    public int PageNumber { get; set; }
    public int SourcePageNumber { get; set; }
    public string SourceFilePath { get; set; } = string.Empty;
    public int Rotation { get; set; }
    public double WidthPoints { get; set; }
    public double HeightPoints { get; set; }
    public bool IsSelected { get; set; }
}

public class CompressSessionState
{
    public bool IsSingleMode { get; set; } = true;
    public int CompressionLevel { get; set; } = 50;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string SharedOutputFolder { get; set; } = string.Empty;
    public int SelectedBatchItemIndex { get; set; } = -1;
    public PdfCompressionRunSummary? SingleRunSummary { get; set; }
    public List<CompressBatchItemSessionState> BatchItems { get; set; } = new();
}

public class CompressBatchItemSessionState
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string OriginalSizeText { get; set; } = "-";
    public string ResultSizeText { get; set; } = "-";
    public bool IsFailed { get; set; }
    public PdfCompressionRunSummary? LastRunSummary { get; set; }
}
