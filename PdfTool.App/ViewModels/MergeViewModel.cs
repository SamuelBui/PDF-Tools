using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Microsoft.Win32;
using PdfTool.App.Commands;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfTool.App.Services;

namespace PdfTool.App.ViewModels;

public class MergeViewModel : BaseViewModel
{
    private const int MaxUndoSteps = 40;

    private readonly IPdfMergeService _service;
    private readonly IAppStatusService _statusService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPdfDocumentInspectorService _inspectorService;
    private readonly IPdfThumbnailService _thumbnailService;
    private readonly Dispatcher _uiDispatcher;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new(StringComparer.OrdinalIgnoreCase);
    private Stack<MergeQueueSnapshot> _undoStack = new();
    private PdfFileItem? _selectedFile;
    private string _outputPath = string.Empty;
    private string _statusMessage = "Add at least one PDF file, arrange the pages, then merge.";
    private bool _lastOperationSucceeded;
    private bool _isBusy;
    private bool _isRestoringUndo;
    private RecentFileItem? _selectedRecentFile;
    private string _previewFilePath = string.Empty;
    private string _previewFileName = "-";
    private string _previewPageCount = "-";
    private string _previewFileSize = "-";
    private string _previewEncryption = "-";
    private string _previewStatus = "Select a file to preview its details.";
    private ImageSource? _previewCoverThumbnail;
    private string _validationSummaryText = "Validation has not run yet.";
    private string _lastOutputPath = string.Empty;
    private JobReportEntry? _selectedReportItem;

    public MergeViewModel(
        IPdfMergeService service,
        IAppStatusService statusService,
        IRecentFilesService recentFilesService,
        IPdfDocumentInspectorService inspectorService,
        IPdfThumbnailService thumbnailService)
    {
        _service = service;
        _statusService = statusService;
        _recentFilesService = recentFilesService;
        _inspectorService = inspectorService;
        _thumbnailService = thumbnailService;
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        RecentFiles = recentFilesService.Files;

        Files = new ObservableCollection<PdfFileItem>();
        Files.CollectionChanged += Files_OnCollectionChanged;

        ReportEntries = new ObservableCollection<JobReportEntry>();

        AddFilesCommand = new RelayCommand(OpenFilesDialog, () => !IsBusy);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => !IsBusy);
        MergeCommand = new RelayCommand(MergeFiles, CanMerge);
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        AddSelectedRecentCommand = new RelayCommand(AddSelectedRecent, () => SelectedRecentFile != null && !IsBusy);
        OpenSelectedFileCommand = new RelayCommand(OpenSelectedFile, () => SelectedFile != null);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => !string.IsNullOrWhiteSpace(LastOutputPath) && File.Exists(LastOutputPath));
        OpenSelectedOutputCommand = new RelayCommand(OpenSelectedOutput, () => !string.IsNullOrWhiteSpace(LastOutputPath) && File.Exists(LastOutputPath));
        ReloadSelectedPreviewCommand = new RelayCommand(ReloadSelectedPreview, () => SelectedFile != null && !IsBusy);
    }

    public ObservableCollection<PdfFileItem> Files { get; }
    public ObservableCollection<JobReportEntry> ReportEntries { get; }
    public ReadOnlyObservableCollection<RecentFileItem> RecentFiles { get; }

    public PdfFileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                RaisePreviewStateChanged();
                RefreshCommands();
                if (_isRestoringUndo)
                {
                    UpdatePreviewFromSelectedFileState();
                }
                else
                {
                    LoadPreviewAsync();
                }
            }
        }
    }

    public RecentFileItem? SelectedRecentFile
    {
        get => _selectedRecentFile;
        set
        {
            if (SetProperty(ref _selectedRecentFile, value))
            {
                AddSelectedRecentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public JobReportEntry? SelectedReportItem
    {
        get => _selectedReportItem;
        set => SetProperty(ref _selectedReportItem, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                RefreshCommands();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool LastOperationSucceeded
    {
        get => _lastOperationSucceeded;
        set => SetProperty(ref _lastOperationSucceeded, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public string PreviewFilePath
    {
        get => _previewFilePath;
        set => SetProperty(ref _previewFilePath, value);
    }

    public string PreviewFileName
    {
        get => _previewFileName;
        set => SetProperty(ref _previewFileName, value);
    }

    public string PreviewPageCount
    {
        get => _previewPageCount;
        set => SetProperty(ref _previewPageCount, value);
    }

    public string PreviewFileSize
    {
        get => _previewFileSize;
        set => SetProperty(ref _previewFileSize, value);
    }

    public string PreviewEncryption
    {
        get => _previewEncryption;
        set => SetProperty(ref _previewEncryption, value);
    }

    public string PreviewStatus
    {
        get => _previewStatus;
        set => SetProperty(ref _previewStatus, value);
    }

    public ImageSource? PreviewCoverThumbnail
    {
        get => _previewCoverThumbnail;
        set => SetProperty(ref _previewCoverThumbnail, value);
    }

    public string ValidationSummaryText
    {
        get => _validationSummaryText;
        set => SetProperty(ref _validationSummaryText, value);
    }

    public string LastOutputPath
    {
        get => _lastOutputPath;
        set
        {
            if (SetProperty(ref _lastOutputPath, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsPreviewPasswordPanelVisible => SelectedFile?.IsEncrypted == true;

    public string PreviewPasswordHint
    {
        get
        {
            if (SelectedFile == null || !SelectedFile.IsEncrypted)
            {
                return string.Empty;
            }

            if (SelectedFile.IsPasswordIncorrect)
            {
                return "Incorrect password. Enter the correct password to preview and merge this file.";
            }

            if (SelectedFile.RequiresPassword)
            {
                return "Password required to preview and merge this file.";
            }

            return "This file is encrypted. You can update the password here if needed.";
        }
    }

    public string QueueSummaryText
    {
        get
        {
            if (Files.Count == 0)
            {
                return "No files in queue.";
            }

            var totalPages = GetTotalPageCount();
            var selectedPages = Files.Sum(file => file.PageThumbnails.Count(page => page.IsSelected));
            var summaryParts = new List<string>
            {
                $"{Files.Count} file(s)",
                $"{totalPages} page(s)"
            };

            if (selectedPages > 0)
            {
                summaryParts.Add($"{selectedPages} selected");
            }

            return string.Join(" | ", summaryParts);
        }
    }

    public bool CanUndo => _undoStack.Count > 0 && !IsBusy;

    public RelayCommand AddFilesCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand MergeCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand AddSelectedRecentCommand { get; }
    public RelayCommand OpenSelectedFileCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }
    public RelayCommand OpenSelectedOutputCommand { get; }
    public RelayCommand ReloadSelectedPreviewCommand { get; }

    private void OpenFilesDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Select PDF files to merge"
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        var uniqueFilesToAdd = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(path => Files.All(existing => !string.Equals(existing.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueFilesToAdd.Count == 0)
        {
            StatusMessage = "No new PDF files were added.";
            LastOperationSucceeded = false;
            return;
        }

        PushUndoState();

        var addedItems = new List<PdfFileItem>();
        foreach (var file in uniqueFilesToAdd)
        {
            var item = new PdfFileItem { FilePath = file };
            Files.Add(item);
            addedItems.Add(item);
        }

        if (Files.Count > 0 && string.IsNullOrWhiteSpace(OutputPath))
        {
            OutputPath = FileNameHelper.CreateMergedFilePath(Files.First().FilePath);
        }

        if (SelectedFile == null && Files.Count > 0)
        {
            SelectedFile = Files.First();
        }

        ValidateQueue();
        StatusMessage = $"{Files.Count} file(s) ready to review.";
        LastOperationSucceeded = false;
        RefreshQueueItemsAsync(addedItems);
    }

    public void RemoveFiles(IReadOnlyList<PdfFileItem> files)
    {
        if (IsBusy || files.Count == 0)
        {
            return;
        }

        var removableFiles = Files.Where(files.Contains).ToList();
        if (removableFiles.Count == 0)
        {
            return;
        }

        PushUndoState();

        foreach (var file in removableFiles)
        {
            Files.Remove(file);
        }

        if (SelectedFile != null && !Files.Contains(SelectedFile))
        {
            SelectedFile = Files.FirstOrDefault();
        }

        StatusMessage = Files.Count == 0
            ? "Merge queue cleared."
            : $"{removableFiles.Count} file(s) removed from the queue.";
        ValidateQueue();
    }

    public void ClearTransientData()
    {
        ClearDropTargets();
        Files.Clear();
        ReportEntries.Clear();
        SelectedFile = null;
        SelectedRecentFile = null;
        SelectedReportItem = null;
        OutputPath = string.Empty;
        LastOutputPath = string.Empty;
        PreviewFileName = "-";
        PreviewFilePath = string.Empty;
        PreviewPageCount = "-";
        PreviewFileSize = "-";
        PreviewEncryption = "-";
        PreviewCoverThumbnail = null;
        PreviewStatus = "Select a file to preview its details.";
        ValidationSummaryText = "Validation has not run yet.";
        _undoStack.Clear();
        _refreshLocks.Clear();
        LastOperationSucceeded = false;
        StatusMessage = "Merge workspace cleared.";
        RaisePreviewStateChanged();
        RaiseMergeQueueStateChanged();
        RefreshCommands();
    }

    public void ReorderFiles(IReadOnlyList<PdfFileItem> draggedItems, PdfFileItem? targetItem)
    {
        if (IsBusy)
        {
            return;
        }

        var orderedDraggedItems = Files.Where(draggedItems.Contains).ToList();
        if (orderedDraggedItems.Count == 0)
        {
            return;
        }

        if (targetItem != null && orderedDraggedItems.Contains(targetItem))
        {
            return;
        }

        PushUndoState();

        var targetIndex = targetItem == null ? Files.Count : Files.IndexOf(targetItem);
        if (targetIndex < 0)
        {
            targetIndex = Files.Count;
        }

        foreach (var item in orderedDraggedItems)
        {
            var itemIndex = Files.IndexOf(item);
            if (itemIndex >= 0 && itemIndex < targetIndex)
            {
                targetIndex--;
            }

            Files.Remove(item);
        }

        for (var index = 0; index < orderedDraggedItems.Count; index++)
        {
            Files.Insert(Math.Min(targetIndex + index, Files.Count), orderedDraggedItems[index]);
        }

        SelectedFile = orderedDraggedItems[0];
        StatusMessage = "Queue order updated.";
        ValidateQueue();
    }

    public void ReorderPages(PdfFileItem parentFile, IReadOnlyList<PdfPageOrganizerItem> draggedPages, PdfPageOrganizerItem? targetPage)
    {
        if (IsBusy || draggedPages.Count == 0 || !Files.Contains(parentFile))
        {
            return;
        }

        var orderedDraggedPages = parentFile.PageThumbnails.Where(draggedPages.Contains).ToList();
        if (orderedDraggedPages.Count == 0)
        {
            return;
        }

        if (targetPage != null && orderedDraggedPages.Contains(targetPage))
        {
            return;
        }

        PushUndoState();

        var targetIndex = targetPage == null ? parentFile.PageThumbnails.Count : parentFile.PageThumbnails.IndexOf(targetPage) + 1;
        if (targetIndex < 0)
        {
            targetIndex = parentFile.PageThumbnails.Count;
        }

        foreach (var page in orderedDraggedPages)
        {
            var pageIndex = parentFile.PageThumbnails.IndexOf(page);
            if (pageIndex >= 0 && pageIndex < targetIndex)
            {
                targetIndex--;
            }

            parentFile.PageThumbnails.Remove(page);
        }

        for (var index = 0; index < orderedDraggedPages.Count; index++)
        {
            parentFile.PageThumbnails.Insert(Math.Min(targetIndex + index, parentFile.PageThumbnails.Count), orderedDraggedPages[index]);
        }

        NormalizePageSequence(parentFile);
        StatusMessage = "Page order updated.";
        RaiseMergeQueueStateChanged();
    }

    public void InsertPagesIntoFile(PdfFileItem targetFile, IReadOnlyList<PdfPageOrganizerItem> draggedPages, PdfPageOrganizerItem? targetPage)
    {
        if (IsBusy || draggedPages.Count == 0 || !Files.Contains(targetFile))
        {
            return;
        }

        var sourceFile = Files.FirstOrDefault(file => file.PageThumbnails.Any(draggedPages.Contains));
        if (sourceFile == null)
        {
            return;
        }

        if (ReferenceEquals(sourceFile, targetFile))
        {
            ReorderPages(targetFile, draggedPages, targetPage);
            return;
        }

        var movingPages = sourceFile.PageThumbnails.Where(draggedPages.Contains).ToList();
        if (movingPages.Count == 0)
        {
            return;
        }

        PushUndoState();

        var targetIndex = targetPage == null ? targetFile.PageThumbnails.Count : targetFile.PageThumbnails.IndexOf(targetPage) + 1;
        if (targetIndex < 0)
        {
            targetIndex = targetFile.PageThumbnails.Count;
        }

        foreach (var page in movingPages)
        {
            sourceFile.PageThumbnails.Remove(page);
        }

        for (var index = 0; index < movingPages.Count; index++)
        {
            targetFile.PageThumbnails.Insert(Math.Min(targetIndex + index, targetFile.PageThumbnails.Count), movingPages[index]);
        }

        NormalizePageSequence(sourceFile);
        NormalizePageSequence(targetFile);
        CleanupEmptyFiles();
        SelectedFile = targetFile;
        StatusMessage = $"Inserted {movingPages.Count} page(s) into {targetFile.FileName}.";
        RaiseMergeQueueStateChanged();
    }

    public void InsertFilesIntoFile(PdfFileItem targetFile, IReadOnlyList<PdfFileItem> draggedFiles, PdfPageOrganizerItem? targetPage)
    {
        if (IsBusy || draggedFiles.Count == 0 || !Files.Contains(targetFile))
        {
            return;
        }

        var sourceFiles = Files
            .Where(draggedFiles.Contains)
            .Where(file => !ReferenceEquals(file, targetFile))
            .ToList();

        if (sourceFiles.Count == 0)
        {
            return;
        }

        PushUndoState();

        var targetIndex = targetPage == null ? targetFile.PageThumbnails.Count : targetFile.PageThumbnails.IndexOf(targetPage) + 1;
        if (targetIndex < 0)
        {
            targetIndex = targetFile.PageThumbnails.Count;
        }

        foreach (var sourceFile in sourceFiles)
        {
            var pagesToMove = sourceFile.PageThumbnails.ToList();
            foreach (var page in pagesToMove)
            {
                sourceFile.PageThumbnails.Remove(page);
            }

            for (var index = 0; index < pagesToMove.Count; index++)
            {
                targetFile.PageThumbnails.Insert(Math.Min(targetIndex + index, targetFile.PageThumbnails.Count), pagesToMove[index]);
            }

            targetIndex += pagesToMove.Count;
            NormalizePageSequence(sourceFile);
        }

        NormalizePageSequence(targetFile);
        CleanupEmptyFiles();
        SelectedFile = targetFile;
        StatusMessage = $"Inserted {sourceFiles.Count} file(s) into {targetFile.FileName}.";
        RaiseMergeQueueStateChanged();
    }

    public void RotateSelectedPages(int delta)
    {
        if (IsBusy)
        {
            return;
        }

        var selectedPages = Files
            .SelectMany(file => file.PageThumbnails)
            .Where(page => page.IsSelected)
            .ToList();

        if (selectedPages.Count == 0)
        {
            return;
        }

        PushUndoState();

        foreach (var page in selectedPages)
        {
            page.Rotation += delta;
        }

        StatusMessage = delta > 0
            ? $"Rotated {selectedPages.Count} selected page(s) to the right."
            : $"Rotated {selectedPages.Count} selected page(s) to the left.";
    }

    public void ClearDropTargets()
    {
        foreach (var file in Files)
        {
            file.IsDropTarget = false;
            foreach (var page in file.PageThumbnails)
            {
                page.IsDropTarget = false;
            }
        }
    }

    public void SetFileDropTarget(PdfFileItem? targetFile)
    {
        ClearDropTargets();
        if (targetFile != null)
        {
            targetFile.IsDropTarget = true;
        }
    }

    public void SetPageDropTarget(PdfFileItem? parentFile, PdfPageOrganizerItem? targetPage)
    {
        ClearDropTargets();
        if (targetPage != null)
        {
            targetPage.IsDropTarget = true;
        }
        else if (parentFile != null)
        {
            parentFile.IsDropTarget = true;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Save merged PDF",
            FileName = string.IsNullOrWhiteSpace(OutputPath) ? "merged.pdf" : Path.GetFileName(OutputPath)
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    private bool CanMerge()
        => Files.Count >= 1 && GetTotalPageCount() > 0 && !string.IsNullOrWhiteSpace(OutputPath) && !IsBusy;

    private async void MergeFiles()
    {
        IsBusy = true;
        _statusService.Start("Merging PDF files...");
        _statusService.Report("Validating queue...", 20);

        var validationInputs = CreateValidationWorkItems();
        var validationOutcome = await Task.Run(() => EvaluateValidation(validationInputs));
        ApplyValidationOutcome(validationOutcome);
        var validation = validationOutcome.Summary;

        if (validation.HasBlockingIssues)
        {
            IsBusy = false;
            LastOperationSucceeded = false;
            StatusMessage = "Resolve the files that are locked, duplicated, invalid, or still require a password before merging.";
            _statusService.Fail("Merge validation failed.");
            AddReport("Merge", string.Join(" | ", Files.Select(x => x.FilePath)), OutputPath, "Failed", StatusMessage);
            return;
        }

        _statusService.Report("Combining selected files...", 60);

        var inputFiles = Files.ToList();
        var result = await Task.Run(() => _service.Merge(inputFiles, OutputPath));

        IsBusy = false;
        LastOperationSucceeded = result.Success;
        StatusMessage = result.Message;

        if (result.Success)
        {
            foreach (var inputFile in inputFiles)
            {
                _recentFilesService.AddFile(inputFile.FilePath);
            }

            LastOutputPath = result.OutputPath ?? OutputPath;
            _statusService.Complete("Merge PDF completed.");
            AddReport("Merge", string.Join(" | ", inputFiles.Select(x => x.FilePath)), LastOutputPath, "Success", string.Empty);
            OpenWithShell(LastOutputPath);
        }
        else
        {
            _statusService.Fail("Merge PDF failed.");
            AddReport("Merge", string.Join(" | ", inputFiles.Select(x => x.FilePath)), OutputPath, "Failed", result.Message);
        }

        ClearSensitiveMergeState();
    }

    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var snapshot = _undoStack.Pop();
        _isRestoringUndo = true;

        try
        {
            ClearDropTargets();
            Files.Clear();

            foreach (var fileSnapshot in snapshot.Files)
            {
                var file = new PdfFileItem
                {
                    FilePath = fileSnapshot.FilePath,
                    Password = fileSnapshot.Password,
                    Thumbnail = fileSnapshot.Thumbnail,
                    PageCount = fileSnapshot.PageCount,
                    IsEncrypted = fileSnapshot.IsEncrypted,
                    RequiresPassword = fileSnapshot.RequiresPassword,
                    IsPasswordIncorrect = fileSnapshot.IsPasswordIncorrect,
                    IsLocked = fileSnapshot.IsLocked,
                    IsDuplicate = fileSnapshot.IsDuplicate,
                    IsValidPdf = fileSnapshot.IsValidPdf,
                    ValidationMessage = fileSnapshot.ValidationMessage
                };

                foreach (var pageSnapshot in fileSnapshot.Pages)
                {
                    file.PageThumbnails.Add(new PdfPageOrganizerItem
                    {
                        PageNumber = pageSnapshot.PageNumber,
                        SourcePageNumber = pageSnapshot.SourcePageNumber,
                        SourceFilePath = pageSnapshot.SourceFilePath,
                        SourcePassword = pageSnapshot.SourcePassword,
                        WidthPoints = pageSnapshot.WidthPoints,
                        HeightPoints = pageSnapshot.HeightPoints,
                        Rotation = pageSnapshot.Rotation,
                        Thumbnail = pageSnapshot.Thumbnail,
                        IsSelected = pageSnapshot.IsSelected
                    });
                }

                Files.Add(file);
            }

            OutputPath = snapshot.OutputPath;
            LastOutputPath = snapshot.LastOutputPath;
            SelectedFile = snapshot.SelectedFileIndex >= 0 && snapshot.SelectedFileIndex < Files.Count
                ? Files[snapshot.SelectedFileIndex]
                : Files.FirstOrDefault();

            ValidationSummaryText = $"{Files.Count} files restored from undo history.";
            StatusMessage = "Undid the previous merge queue action.";
            LastOperationSucceeded = false;
            RaiseMergeQueueStateChanged();
        }
        finally
        {
            _isRestoringUndo = false;
            RefreshCommands();
        }
    }

    private void AddSelectedRecent()
    {
        if (SelectedRecentFile == null)
        {
            return;
        }

        AddFiles(new[] { SelectedRecentFile.FilePath });
        StatusMessage = "Recent file added to merge list.";
    }

    private async void LoadPreviewAsync()
    {
        if (SelectedFile == null)
        {
            UpdatePreviewFromSelectedFileState();
            return;
        }

        var selectedFile = SelectedFile;
        var selectedPath = selectedFile.FilePath;
        PreviewFileName = selectedFile.FileName;
        PreviewFilePath = selectedPath;
        PreviewStatus = "Loading preview...";
        RaisePreviewStateChanged();

        if (selectedFile.PageThumbnails.Count == 0)
        {
            await RefreshQueueItemAsync(selectedFile, loadThumbnail: true, refreshValidation: false);
        }

        if (!ReferenceEquals(SelectedFile, selectedFile))
        {
            return;
        }

        PreviewFileName = selectedFile.FileName;
        PreviewFilePath = selectedFile.FilePath;
        PreviewPageCount = selectedFile.PageCount?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";
        PreviewFileSize = File.Exists(selectedPath)
            ? FormatFileSize(new FileInfo(selectedPath).Length)
            : "Unavailable";
        PreviewEncryption = selectedFile.IsEncrypted
            ? selectedFile.RequiresPassword || selectedFile.IsPasswordIncorrect
                ? "Encrypted (locked)"
                : "Encrypted (unlocked)"
            : "Not encrypted";

        if (selectedFile.IsLocked || selectedFile.IsDuplicate || !selectedFile.IsValidPdf)
        {
            PreviewCoverThumbnail = null;
            PreviewStatus = selectedFile.ValidationMessage;
            RaisePreviewStateChanged();
            return;
        }

        if (selectedFile.RequiresPassword || selectedFile.IsPasswordIncorrect)
        {
            PreviewCoverThumbnail = null;
            PreviewStatus = selectedFile.ValidationMessage;
            RaisePreviewStateChanged();
            return;
        }

        var thumbnails = selectedFile.PageThumbnails.Count > 0
            ? selectedFile.PageThumbnails.Select(page => new PdfPageThumbnailResult
            {
                PageNumber = page.SourcePageNumber > 0 ? page.SourcePageNumber : page.PageNumber,
                Thumbnail = page.Thumbnail
            }).ToList()
            : await Task.Run(() => _thumbnailService.RenderDocumentThumbnails(selectedPath, 160, 220, 1, selectedFile.Password));
        if (!ReferenceEquals(SelectedFile, selectedFile))
        {
            return;
        }

        PreviewCoverThumbnail = thumbnails.FirstOrDefault()?.Thumbnail ?? selectedFile.Thumbnail;
        PreviewStatus = selectedFile.ValidationMessage;
        RaisePreviewStateChanged();
    }

    private void UpdatePreviewFromSelectedFileState()
    {
        if (SelectedFile == null)
        {
            PreviewFileName = "-";
            PreviewFilePath = string.Empty;
            PreviewPageCount = "-";
            PreviewFileSize = "-";
            PreviewEncryption = "-";
            PreviewCoverThumbnail = null;
            PreviewStatus = "Select a file to preview its details.";
            RaisePreviewStateChanged();
            return;
        }

        var selectedFile = SelectedFile;
        PreviewFileName = selectedFile.FileName;
        PreviewFilePath = selectedFile.FilePath;
        PreviewPageCount = selectedFile.PageCount?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";
        PreviewFileSize = File.Exists(selectedFile.FilePath)
            ? FormatFileSize(new FileInfo(selectedFile.FilePath).Length)
            : "Unavailable";
        PreviewEncryption = selectedFile.IsEncrypted
            ? selectedFile.RequiresPassword || selectedFile.IsPasswordIncorrect
                ? "Encrypted (locked)"
                : "Encrypted (unlocked)"
            : "Not encrypted";

        PreviewCoverThumbnail = selectedFile.PageThumbnails.Select(page => page.Thumbnail).FirstOrDefault(thumbnail => thumbnail != null)
                              ?? selectedFile.Thumbnail;
        PreviewStatus = selectedFile.ValidationMessage;
        RaisePreviewStateChanged();
    }

    private async void ReloadSelectedPreview()
    {
        if (SelectedFile == null)
        {
            return;
        }

        await RefreshQueueItemAsync(SelectedFile, loadThumbnail: true, refreshValidation: true);
        LoadPreviewAsync();
    }

    private void ValidateQueue()
    {
        var validationOutcome = EvaluateValidation(CreateValidationWorkItems());
        ApplyValidationOutcome(validationOutcome);
    }

    private List<MergeValidationWorkItem> CreateValidationWorkItems()
    {
        var duplicatePaths = Files
            .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Files
            .Select(file => new MergeValidationWorkItem(
                file,
                file.FilePath,
                file.Password,
                duplicatePaths.Contains(file.FilePath)))
            .ToList();
    }

    private MergeValidationOutcome EvaluateValidation(IReadOnlyList<MergeValidationWorkItem> items)
    {
        var summary = new MergeValidationSummary
        {
            TotalFiles = items.Count
        };

        var results = new List<MergeValidationResult>(items.Count);

        foreach (var item in items)
        {
            if (item.IsDuplicate)
            {
                summary.DuplicateFiles++;
                results.Add(new MergeValidationResult(
                    item.File,
                    item.IsDuplicate,
                    IsLocked: false,
                    AccessError: null,
                    Info: null));
                continue;
            }

            if (!FileAccessHelper.TryValidateReadableFile(item.FilePath, out var inputError))
            {
                summary.LockedFiles++;
                results.Add(new MergeValidationResult(
                    item.File,
                    item.IsDuplicate,
                    IsLocked: true,
                    AccessError: inputError,
                    Info: null));
                continue;
            }

            var info = _inspectorService.Inspect(item.FilePath, item.Password);
            results.Add(new MergeValidationResult(
                item.File,
                item.IsDuplicate,
                IsLocked: false,
                AccessError: null,
                Info: info));

            if (!info.IsPdf)
            {
                summary.InvalidPdfFiles++;
                continue;
            }

            if (info.RequiresPassword)
            {
                summary.PasswordRequiredFiles++;
                continue;
            }

            if (info.IsPasswordIncorrect)
            {
                summary.InvalidPasswordFiles++;
                continue;
            }

            summary.ReadyFiles++;
        }

        return new MergeValidationOutcome(summary, results);
    }

    private void ApplyValidationOutcome(MergeValidationOutcome outcome)
    {
        RunOnUiThread(() =>
        {
            foreach (var result in outcome.Results)
            {
                if (!Files.Contains(result.File))
                {
                    continue;
                }

                var file = result.File;
                file.IsDuplicate = result.IsDuplicate;
            file.IsLocked = false;
            file.RequiresPassword = false;
            file.IsPasswordIncorrect = false;
            file.IsValidPdf = true;

                if (file.IsDuplicate)
                {
                    file.ValidationMessage = "Duplicate file in queue. Remove the extra copy.";
                    continue;
                }

                if (result.IsLocked)
                {
                    file.IsLocked = true;
                    file.ValidationMessage = result.AccessError ?? "File is unavailable.";
                    continue;
                }

                if (result.Info != null)
                {
                    ApplyInspection(file, result.Info);
                }
            }

            ValidationSummaryText = $"{outcome.Summary.TotalFiles} files: {outcome.Summary.SummaryText}";
            RaisePreviewStateChanged();
            RaiseMergeQueueStateChanged();
        });
    }

    private async void RefreshQueueItemsAsync(IReadOnlyList<PdfFileItem> items)
    {
        foreach (var item in items)
        {
            await RefreshQueueItemAsync(item, loadThumbnail: true, refreshValidation: false);
        }

        ValidateQueue();
    }

    private async Task RefreshQueueItemAsync(PdfFileItem item, bool loadThumbnail, bool refreshValidation)
    {
        if (!Files.Contains(item))
        {
            return;
        }

        var refreshLock = _refreshLocks.GetOrAdd(item.FilePath, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync();

        try
        {
        var filePath = item.FilePath;
        item.IsDuplicate = false;
        item.IsLocked = false;
        item.RequiresPassword = false;
        item.IsPasswordIncorrect = false;
        item.IsValidPdf = true;
        item.ValidationMessage = "Checking file...";
        item.PageThumbnails.Clear();

        if (!FileAccessHelper.TryValidateReadableFile(filePath, out var inputError))
        {
            item.IsLocked = true;
            item.ValidationMessage = inputError;
            item.Thumbnail = null;
            if (ReferenceEquals(SelectedFile, item))
            {
                RaisePreviewStateChanged();
            }

            if (refreshValidation)
            {
                ValidateQueue();
            }

            return;
        }

        var info = await Task.Run(() => _inspectorService.Inspect(filePath, item.Password));
        if (!Files.Contains(item))
        {
            return;
        }

        ApplyInspection(item, info);

        if (loadThumbnail && info.IsPdf && !info.RequiresPassword && !info.IsPasswordIncorrect)
        {
            var thumbnails = await Task.Run(() => _thumbnailService.RenderDocumentThumbnails(filePath, 72, 92, null, item.Password));
            if (Files.Contains(item))
            {
                foreach (var thumbnail in thumbnails)
                {
                    item.PageThumbnails.Add(new PdfPageOrganizerItem
                    {
                        PageNumber = thumbnail.PageNumber,
                        SourcePageNumber = thumbnail.PageNumber,
                        SourceFilePath = filePath,
                        SourcePassword = item.Password,
                        Thumbnail = thumbnail.Thumbnail
                    });
                }

                NormalizePageSequence(item);
            }
        }
        else
        {
            item.Thumbnail = null;
        }

        if (ReferenceEquals(SelectedFile, item))
        {
            RaisePreviewStateChanged();
        }

        if (refreshValidation)
        {
            ValidateQueue();
        }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private static void ApplyInspection(PdfFileItem file, PdfDocumentInfo info)
    {
        file.PageCount = info.PageCount;
        file.IsEncrypted = info.IsEncrypted;
        file.RequiresPassword = info.RequiresPassword;
        file.IsPasswordIncorrect = info.IsPasswordIncorrect;
        file.IsValidPdf = info.IsPdf;
        file.ValidationMessage = info.StatusMessage;
    }

    private void CleanupEmptyFiles()
    {
        foreach (var file in Files.Where(file => file.PageThumbnails.Count == 0).ToList())
        {
            Files.Remove(file);
        }

        if (SelectedFile != null && !Files.Contains(SelectedFile))
        {
            SelectedFile = Files.FirstOrDefault();
        }
    }

    private static void NormalizePageSequence(PdfFileItem file)
    {
        file.PageCount = file.PageThumbnails.Count;
        file.Thumbnail = file.PageThumbnails.FirstOrDefault()?.Thumbnail;

        for (var index = 0; index < file.PageThumbnails.Count; index++)
        {
            file.PageThumbnails[index].PageNumber = index + 1;
        }
    }

    private void OpenSelectedFile()
    {
        if (SelectedFile == null)
        {
            return;
        }

        OpenWithShell(SelectedFile.FilePath);
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(LastOutputPath) || !File.Exists(LastOutputPath))
        {
            return;
        }

        var folderPath = Path.GetDirectoryName(LastOutputPath);
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            OpenWithShell(folderPath);
        }
    }

    private void OpenSelectedOutput()
    {
        if (!string.IsNullOrWhiteSpace(LastOutputPath) && File.Exists(LastOutputPath))
        {
            OpenWithShell(LastOutputPath);
        }
    }

    private void AddReport(string action, string input, string output, string result, string errorMessage)
    {
        ReportEntries.Insert(0, new JobReportEntry
        {
            TimestampLocal = DateTime.Now,
            Action = action,
            Input = input,
            Output = output,
            Result = result,
            ErrorMessage = errorMessage
        });
    }

    private void RaisePreviewStateChanged()
    {
        if (!CheckAccess())
        {
            RunOnUiThread(RaisePreviewStateChanged);
            return;
        }

        OnPropertyChanged(nameof(IsPreviewPasswordPanelVisible));
        OnPropertyChanged(nameof(PreviewPasswordHint));
        ReloadSelectedPreviewCommand.RaiseCanExecuteChanged();
    }

    private static void OpenWithShell(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private void PushUndoState()
    {
        if (_isRestoringUndo)
        {
            return;
        }

        _undoStack.Push(CaptureSnapshot());
        if (_undoStack.Count > MaxUndoSteps)
        {
            var recentSnapshots = _undoStack.Take(MaxUndoSteps).Reverse().ToList();
            _undoStack = new Stack<MergeQueueSnapshot>(recentSnapshots);
        }

        RefreshCommands();
    }

    private MergeQueueSnapshot CaptureSnapshot()
    {
        return new MergeQueueSnapshot(
            OutputPath,
            LastOutputPath,
            SelectedFile != null ? Files.IndexOf(SelectedFile) : -1,
            Files.Select(file => new MergeFileSnapshot(
                file.FilePath,
                file.Password,
                file.Thumbnail,
                file.PageCount,
                file.IsEncrypted,
                file.RequiresPassword,
                file.IsPasswordIncorrect,
                file.IsLocked,
                file.IsDuplicate,
                file.IsValidPdf,
                file.ValidationMessage,
                file.PageThumbnails.Select(page => new MergePageSnapshot(
                    page.PageNumber,
                    page.SourcePageNumber,
                    page.SourceFilePath,
                    page.SourcePassword,
                    page.Rotation,
                    page.WidthPoints,
                    page.HeightPoints,
                    page.IsSelected,
                    page.Thumbnail)).ToList())).ToList());
    }

    public MergeSessionState CaptureSessionState()
    {
        return new MergeSessionState
        {
            OutputPath = OutputPath,
            LastOutputPath = LastOutputPath,
            SelectedFileIndex = SelectedFile != null ? Files.IndexOf(SelectedFile) : -1,
            Files = Files.Select(file => new MergeFileSessionState
            {
                FilePath = file.FilePath,
                PageCount = file.PageCount,
                IsEncrypted = file.IsEncrypted,
                RequiresPassword = file.RequiresPassword,
                IsPasswordIncorrect = file.IsPasswordIncorrect,
                IsLocked = file.IsLocked,
                IsDuplicate = file.IsDuplicate,
                IsValidPdf = file.IsValidPdf,
                ValidationMessage = file.ValidationMessage,
                Pages = file.PageThumbnails.Select(page => new MergePageSessionState
                {
                    PageNumber = page.PageNumber,
                    SourcePageNumber = page.SourcePageNumber,
                    SourceFilePath = page.SourceFilePath,
                    Rotation = page.Rotation,
                    WidthPoints = page.WidthPoints,
                    HeightPoints = page.HeightPoints,
                    IsSelected = page.IsSelected
                }).ToList()
            }).ToList()
        };
    }

    public void RestoreSessionState(MergeSessionState? state)
    {
        if (state == null)
        {
            return;
        }

        _isRestoringUndo = true;

        try
        {
            ClearDropTargets();
            Files.Clear();

            foreach (var fileState in state.Files)
            {
                var file = new PdfFileItem
                {
                    FilePath = fileState.FilePath,
                    PageCount = fileState.PageCount,
                    IsEncrypted = fileState.IsEncrypted,
                    RequiresPassword = fileState.RequiresPassword,
                    IsPasswordIncorrect = fileState.IsPasswordIncorrect,
                    IsLocked = fileState.IsLocked,
                    IsDuplicate = fileState.IsDuplicate,
                    IsValidPdf = fileState.IsValidPdf,
                    ValidationMessage = fileState.ValidationMessage
                };

                foreach (var pageState in fileState.Pages)
                {
                    file.PageThumbnails.Add(new PdfPageOrganizerItem
                    {
                        PageNumber = pageState.PageNumber,
                        SourcePageNumber = pageState.SourcePageNumber,
                        SourceFilePath = pageState.SourceFilePath,
                        WidthPoints = pageState.WidthPoints,
                        HeightPoints = pageState.HeightPoints,
                        Rotation = pageState.Rotation,
                        IsSelected = pageState.IsSelected
                    });
                }

                NormalizePageSequence(file);
                Files.Add(file);
            }

            OutputPath = state.OutputPath ?? string.Empty;
            LastOutputPath = state.LastOutputPath ?? string.Empty;
            _undoStack.Clear();
            SelectedFile = state.SelectedFileIndex >= 0 && state.SelectedFileIndex < Files.Count
                ? Files[state.SelectedFileIndex]
                : Files.FirstOrDefault();

            ValidateQueue();
            StatusMessage = Files.Count == 0 ? "Merge session restored." : "Merge queue session restored. Passwords must be re-entered.";
            LastOperationSucceeded = false;
            RefreshCommands();
        }
        finally
        {
            _isRestoringUndo = false;
        }

        _ = RefreshRestoredQueueThumbnailsAsync(Files.ToList());
    }

    private async Task RefreshRestoredQueueThumbnailsAsync(IReadOnlyList<PdfFileItem> files)
    {
        var pageGroups = files
            .SelectMany(file => file.PageThumbnails)
            .Where(page => !string.IsNullOrWhiteSpace(page.SourceFilePath) && File.Exists(page.SourceFilePath))
            .GroupBy(page => (page.SourceFilePath, page.SourcePassword));

        foreach (var group in pageGroups)
        {
            var thumbnails = await Task.Run(() =>
                _thumbnailService.RenderDocumentThumbnails(group.Key.SourceFilePath, 72, 92, null, group.Key.SourcePassword));
            var thumbnailMap = thumbnails.ToDictionary(item => item.PageNumber, item => item.Thumbnail);

            foreach (var page in group)
            {
                if (thumbnailMap.TryGetValue(page.SourcePageNumber, out var thumbnail))
                {
                    page.Thumbnail = thumbnail;
                }
            }
        }

        foreach (var file in files)
        {
            NormalizePageSequence(file);
        }

        UpdatePreviewFromSelectedFileState();
    }

    private void Files_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (PdfFileItem file in e.OldItems)
            {
                DetachFile(file);
            }
        }

        if (e.NewItems != null)
        {
            foreach (PdfFileItem file in e.NewItems)
            {
                AttachFile(file);
            }
        }

        RefreshCommands();
        RaiseMergeQueueStateChanged();
    }

    private void AttachFile(PdfFileItem file)
    {
        file.PropertyChanged += File_OnPropertyChanged;
        file.PageThumbnails.CollectionChanged += PageThumbnails_OnCollectionChanged;

        foreach (var page in file.PageThumbnails)
        {
            AttachPage(page);
        }
    }

    private void DetachFile(PdfFileItem file)
    {
        file.PropertyChanged -= File_OnPropertyChanged;
        file.PageThumbnails.CollectionChanged -= PageThumbnails_OnCollectionChanged;

        foreach (var page in file.PageThumbnails)
        {
            DetachPage(page);
        }
    }

    private void PageThumbnails_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (PdfPageOrganizerItem page in e.OldItems)
            {
                DetachPage(page);
            }
        }

        if (e.NewItems != null)
        {
            foreach (PdfPageOrganizerItem page in e.NewItems)
            {
                AttachPage(page);
            }
        }

        RaiseMergeQueueStateChanged();
    }

    private void AttachPage(PdfPageOrganizerItem page)
    {
        page.PropertyChanged += Page_OnPropertyChanged;
    }

    private void DetachPage(PdfPageOrganizerItem page)
    {
        page.PropertyChanged -= Page_OnPropertyChanged;
    }

    private void File_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!CheckAccess())
        {
            RunOnUiThread(() => File_OnPropertyChanged(sender, e));
            return;
        }

        if (sender is not PdfFileItem file)
        {
            return;
        }

        if (e.PropertyName == nameof(PdfFileItem.Password))
        {
            UpdateSourcePasswords(file);
        }

        if (ReferenceEquals(SelectedFile, file))
        {
            RaisePreviewStateChanged();
        }

        RaiseMergeQueueStateChanged();
    }

    private void Page_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!CheckAccess())
        {
            RunOnUiThread(() => Page_OnPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName == nameof(PdfPageOrganizerItem.IsSelected) ||
            e.PropertyName == nameof(PdfPageOrganizerItem.PageNumber) ||
            e.PropertyName == nameof(PdfPageOrganizerItem.Rotation))
        {
            RaiseMergeQueueStateChanged();
        }
    }

    private void UpdateSourcePasswords(PdfFileItem file)
    {
        foreach (var queueFile in Files)
        {
            foreach (var page in queueFile.PageThumbnails)
            {
                if (string.IsNullOrWhiteSpace(page.SourceFilePath) ||
                    string.Equals(page.SourceFilePath, file.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    page.SourcePassword = file.Password;
                }
            }
        }
    }

    private void ClearSensitiveMergeState()
    {
        foreach (var file in Files)
        {
            file.Password = string.Empty;

            foreach (var page in file.PageThumbnails)
            {
                page.SourcePassword = string.Empty;
            }
        }

        _undoStack.Clear();
        RaisePreviewStateChanged();
        RaiseMergeQueueStateChanged();
    }

    private int GetTotalPageCount()
        => Files.Sum(file => file.PageThumbnails.Count > 0 ? file.PageThumbnails.Count : file.PageCount ?? 0);

    private void RaiseMergeQueueStateChanged()
    {
        if (!CheckAccess())
        {
            RunOnUiThread(RaiseMergeQueueStateChanged);
            return;
        }

        OnPropertyChanged(nameof(QueueSummaryText));
        RefreshCommands();
    }

    private static string FormatFileSize(long bytes)
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

    private void RefreshCommands()
    {
        if (!CheckAccess())
        {
            RunOnUiThread(RefreshCommands);
            return;
        }

        AddFilesCommand.RaiseCanExecuteChanged();
        BrowseOutputCommand.RaiseCanExecuteChanged();
        MergeCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        AddSelectedRecentCommand.RaiseCanExecuteChanged();
        OpenSelectedFileCommand.RaiseCanExecuteChanged();
        OpenOutputFolderCommand.RaiseCanExecuteChanged();
        OpenSelectedOutputCommand.RaiseCanExecuteChanged();
        ReloadSelectedPreviewCommand.RaiseCanExecuteChanged();
    }

    private bool CheckAccess() => _uiDispatcher.CheckAccess();

    private void RunOnUiThread(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        _uiDispatcher.Invoke(action);
    }

    private sealed record MergeQueueSnapshot(
        string OutputPath,
        string LastOutputPath,
        int SelectedFileIndex,
        IReadOnlyList<MergeFileSnapshot> Files);

    private sealed record MergeFileSnapshot(
        string FilePath,
        string Password,
        ImageSource? Thumbnail,
        int? PageCount,
        bool IsEncrypted,
        bool RequiresPassword,
        bool IsPasswordIncorrect,
        bool IsLocked,
        bool IsDuplicate,
        bool IsValidPdf,
        string ValidationMessage,
        IReadOnlyList<MergePageSnapshot> Pages);

    private sealed record MergeValidationWorkItem(
        PdfFileItem File,
        string FilePath,
        string Password,
        bool IsDuplicate);

    private sealed record MergeValidationResult(
        PdfFileItem File,
        bool IsDuplicate,
        bool IsLocked,
        string? AccessError,
        PdfDocumentInfo? Info);

    private sealed record MergeValidationOutcome(
        MergeValidationSummary Summary,
        IReadOnlyList<MergeValidationResult> Results);

    private sealed record MergePageSnapshot(
        int PageNumber,
        int SourcePageNumber,
        string SourceFilePath,
        string SourcePassword,
        int Rotation,
        double WidthPoints,
        double HeightPoints,
        bool IsSelected,
        ImageSource? Thumbnail);
}
