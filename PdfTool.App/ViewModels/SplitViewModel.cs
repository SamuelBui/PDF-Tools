using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using Microsoft.Win32;
using PdfTool.App.Commands;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfTool.App.Services;
using WinForms = System.Windows.Forms;

namespace PdfTool.App.ViewModels;

public class SplitViewModel : BaseViewModel
{
    private readonly IPdfSplitService _service;
    private readonly IPdfThumbnailService _thumbnailService;
    private readonly IAppStatusService _statusService;
    private readonly IRecentFilesService _recentFilesService;
    private string _inputPath = string.Empty;
    private string _outputFolder = string.Empty;
    private string _documentPassword = string.Empty;
    private string _statusMessage = "Choose a PDF to organize pages.";
    private bool _lastOperationSucceeded;
    private bool _isBusy;
    private SplitSelectionPreset _selectionPreset = SplitSelectionPreset.EveryPage;
    private SplitOutputStrategy _outputStrategy = SplitOutputStrategy.SeparateFiles;
    private string _pageSelectionInput = string.Empty;
    private string _pageCountText = "0 pages";
    private string _selectionSummary = "No document loaded.";
    private string _outputPreviewSummary = "Output summary will appear here.";
    private string _validationMessage = "Choose a PDF to begin.";
    private string _documentLoadMessage = "Choose a PDF to begin.";
    private string _documentEncryptionText = "Not loaded";
    private string _documentPasswordHint = "Load a PDF to inspect its protection status.";
    private bool _isApplyingSelectionPreset;
    private bool _isDocumentEncrypted;
    private bool _requiresDocumentPassword;
    private bool _isDocumentPasswordIncorrect;
    private bool _hasOwnerLevelAccess;
    private long _inputFileSizeBytes;

    public SplitViewModel(
        IPdfSplitService service,
        IPdfThumbnailService thumbnailService,
        IAppStatusService statusService,
        IRecentFilesService recentFilesService)
    {
        _service = service;
        _thumbnailService = thumbnailService;
        _statusService = statusService;
        _recentFilesService = recentFilesService;
        Pages = new ObservableCollection<PdfPageOrganizerItem>();
        OutputPreviewItems = new ObservableCollection<SplitOutputPreviewItem>();
        Pages.CollectionChanged += PagesOnCollectionChanged;

        BrowseInputCommand = new RelayCommand(BrowseInput);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        ReloadProtectedDocumentCommand = new RelayCommand(
            ReloadProtectedDocument,
            () => !IsBusy && !string.IsNullOrWhiteSpace(InputPath) && IsDocumentPasswordPanelVisible);
        ExtractSelectedCommand = new RelayCommand(ExtractSelectedPages, CanRunOrganizerAction);
        RemoveSelectedPagesCommand = new RelayCommand(RemoveSelectedPages, CanRunOrganizerAction);
        RotateLeftCommand = new RelayCommand(() => RotateSelectedPages(-90), CanRunOrganizerAction);
        RotateRightCommand = new RelayCommand(() => RotateSelectedPages(90), CanRunOrganizerAction);
        SelectAllPagesCommand = new RelayCommand(SelectAllPages, () => !IsBusy && Pages.Count > 0);
        ClearPageSelectionCommand = new RelayCommand(ClearPageSelection, () => !IsBusy && SelectedPages.Any());
        ApplySelectionPresetCommand = new RelayCommand(ApplySelectionPreset, () => !IsBusy && Pages.Count > 0);
    }

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (SetProperty(ref _inputPath, value))
            {
                ClearDocumentPasswords();
                if (string.IsNullOrWhiteSpace(OutputFolder) && !string.IsNullOrWhiteSpace(value))
                {
                    OutputFolder = FileNameHelper.CreateSplitFolderPath(value);
                }

                LoadDocumentPages();
                RefreshCommands();
            }
        }
    }

    public string DocumentPassword
    {
        get => _documentPassword;
        set
        {
            if (SetProperty(ref _documentPassword, value))
            {
                UpdateValidationMessage();
                RefreshCommands();
            }
        }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (SetProperty(ref _outputFolder, value))
            {
                UpdateNamingPreview();
                UpdateValidationMessage();
                RefreshCommands();
            }
        }
    }

    public SplitSelectionPreset SelectionPreset
    {
        get => _selectionPreset;
        set
        {
            if (SetProperty(ref _selectionPreset, value))
            {
                OnPropertyChanged(nameof(IsEveryPagePreset));
                OnPropertyChanged(nameof(IsOddPagesPreset));
                OnPropertyChanged(nameof(IsEvenPagesPreset));
                OnPropertyChanged(nameof(IsPageListPreset));
                OnPropertyChanged(nameof(IsCustomSelectionPreset));
                ApplySelectionPreset();
                RefreshCommands();
            }
        }
    }

    public SplitOutputStrategy OutputStrategy
    {
        get => _outputStrategy;
        set
        {
            if (SetProperty(ref _outputStrategy, value))
            {
                OnPropertyChanged(nameof(IsSeparateFilesStrategy));
                OnPropertyChanged(nameof(IsRangeFilesStrategy));
                OnPropertyChanged(nameof(IsSingleFileStrategy));
                UpdateNamingPreview();
                RefreshCommands();
            }
        }
    }

    public string PageSelectionInput
    {
        get => _pageSelectionInput;
        set
        {
            if (SetProperty(ref _pageSelectionInput, value))
            {
                if (!_isApplyingSelectionPreset && !string.IsNullOrWhiteSpace(value) && !IsPageListPreset)
                {
                    SelectionPreset = SplitSelectionPreset.PageList;
                    return;
                }

                if (IsPageListPreset)
                {
                    ApplySelectionPreset();
                }
                else
                {
                    UpdateValidationMessage();
                }

                RefreshCommands();
            }
        }
    }

    public bool IsEveryPagePreset
    {
        get => SelectionPreset == SplitSelectionPreset.EveryPage;
        set
        {
            if (value)
            {
                SelectionPreset = SplitSelectionPreset.EveryPage;
            }
        }
    }

    public bool IsOddPagesPreset
    {
        get => SelectionPreset == SplitSelectionPreset.OddPages;
        set
        {
            if (value)
            {
                SelectionPreset = SplitSelectionPreset.OddPages;
            }
        }
    }

    public bool IsEvenPagesPreset
    {
        get => SelectionPreset == SplitSelectionPreset.EvenPages;
        set
        {
            if (value)
            {
                SelectionPreset = SplitSelectionPreset.EvenPages;
            }
        }
    }

    public bool IsPageListPreset
    {
        get => SelectionPreset == SplitSelectionPreset.PageList;
        set
        {
            if (value)
            {
                SelectionPreset = SplitSelectionPreset.PageList;
            }
        }
    }

    public bool IsCustomSelectionPreset
    {
        get => SelectionPreset == SplitSelectionPreset.CustomSelection;
        set
        {
            if (value)
            {
                SelectionPreset = SplitSelectionPreset.CustomSelection;
            }
        }
    }

    public bool IsSeparateFilesStrategy
    {
        get => OutputStrategy == SplitOutputStrategy.SeparateFiles;
        set
        {
            if (value)
            {
                OutputStrategy = SplitOutputStrategy.SeparateFiles;
            }
        }
    }

    public bool IsRangeFilesStrategy
    {
        get => OutputStrategy == SplitOutputStrategy.RangeFiles;
        set
        {
            if (value)
            {
                OutputStrategy = SplitOutputStrategy.RangeFiles;
            }
        }
    }

    public bool IsSingleFileStrategy
    {
        get => OutputStrategy == SplitOutputStrategy.SingleFile;
        set
        {
            if (value)
            {
                OutputStrategy = SplitOutputStrategy.SingleFile;
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

    public string PageCountText
    {
        get => _pageCountText;
        set => SetProperty(ref _pageCountText, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        set => SetProperty(ref _selectionSummary, value);
    }

    public string OutputPreviewSummary
    {
        get => _outputPreviewSummary;
        set => SetProperty(ref _outputPreviewSummary, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public string DocumentEncryptionText
    {
        get => _documentEncryptionText;
        set => SetProperty(ref _documentEncryptionText, value);
    }

    public string DocumentPasswordHint
    {
        get => _documentPasswordHint;
        set => SetProperty(ref _documentPasswordHint, value);
    }

    public ObservableCollection<PdfPageOrganizerItem> Pages { get; }
    public ObservableCollection<SplitOutputPreviewItem> OutputPreviewItems { get; }
    public IReadOnlyList<PdfPageOrganizerItem> SelectedPages => Pages.Where(page => page.IsSelected).ToList();
    public bool IsDocumentPasswordPanelVisible => _isDocumentEncrypted || _requiresDocumentPassword || _isDocumentPasswordIncorrect;

    public RelayCommand BrowseInputCommand { get; }
    public RelayCommand BrowseOutputFolderCommand { get; }
    public RelayCommand ReloadProtectedDocumentCommand { get; }
    public RelayCommand ExtractSelectedCommand { get; }
    public RelayCommand RemoveSelectedPagesCommand { get; }
    public RelayCommand RotateLeftCommand { get; }
    public RelayCommand RotateRightCommand { get; }
    public RelayCommand SelectAllPagesCommand { get; }
    public RelayCommand ClearPageSelectionCommand { get; }
    public RelayCommand ApplySelectionPresetCommand { get; }

    private void BrowseInput()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Select PDF file"
        };

        if (dialog.ShowDialog() == true)
        {
            InputPath = dialog.FileName;
            OutputFolder = FileNameHelper.CreateSplitFolderPath(InputPath);
            StatusMessage = "Input file loaded into Page Organizer.";
            LastOperationSucceeded = false;
        }
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            OutputFolder = dialog.SelectedPath;
        }
    }

    private void ReloadProtectedDocument()
    {
        LoadDocumentPages();
    }

    private void LoadDocumentPages()
    {
        Pages.Clear();

        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
        {
            UpdateDocumentSecurityState(null);
            _documentLoadMessage = "Choose a PDF to begin.";
            _inputFileSizeBytes = 0;
            PageCountText = "0 pages";
            SelectionSummary = "No document loaded.";
            OutputPreviewSummary = "Output summary will appear here.";
            OutputPreviewItems.Clear();
            ValidationMessage = "Choose a PDF to begin.";
            return;
        }

        _inputFileSizeBytes = new FileInfo(InputPath).Length;
        var info = _service.LoadDocumentInfo(InputPath, GetEffectiveAccessPassword());
        UpdateDocumentSecurityState(info);
        if (!info.IsValidPdf || info.RequiresPassword || info.IsPasswordIncorrect || (info.IsEncrypted && !info.HasOwnerPermissions) || info.PageCount == 0)
        {
            _documentLoadMessage = string.IsNullOrWhiteSpace(info.StatusMessage)
                ? "Unable to load pages from this PDF."
                : info.StatusMessage;
            if (info.IsEncrypted && !info.RequiresPassword && !info.IsPasswordIncorrect && !info.HasOwnerPermissions)
            {
                _documentLoadMessage = "Owner password is required to organize pages from this protected PDF.";
            }
            PageCountText = "0 pages";
            SelectionSummary = "No document loaded.";
            OutputPreviewSummary = "Output summary will appear here.";
            OutputPreviewItems.Clear();
            ValidationMessage = _documentLoadMessage;
            StatusMessage = _documentLoadMessage;
            LastOperationSucceeded = false;
            RefreshCommands();
            return;
        }

        _documentLoadMessage = string.Empty;
        foreach (var page in info.Pages)
        {
            page.PropertyChanged += PageOnPropertyChanged;
            Pages.Add(page);
        }

        PageSelectionInput = string.Empty;
        PageCountText = $"{info.PageCount} page(s)";
        ApplySelectionPreset();
        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        LoadThumbnailsAsync(InputPath, GetEffectiveAccessPassword());
    }

    private void ApplySelectionPreset()
    {
        if (Pages.Count == 0)
        {
            return;
        }

        _isApplyingSelectionPreset = true;
        try
        {
            foreach (var page in Pages)
            {
                page.IsSelected = SelectionPreset switch
                {
                    SplitSelectionPreset.EveryPage => true,
                    SplitSelectionPreset.OddPages => page.PageNumber % 2 == 1,
                    SplitSelectionPreset.EvenPages => page.PageNumber % 2 == 0,
                    SplitSelectionPreset.PageList => IsPageNumberInSelectionInput(page.PageNumber),
                    _ => page.IsSelected
                };
            }
        }
        finally
        {
            _isApplyingSelectionPreset = false;
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private void SelectAllPages()
    {
        SelectionPreset = SplitSelectionPreset.CustomSelection;
        foreach (var page in Pages)
        {
            page.IsSelected = true;
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private void ClearPageSelection()
    {
        SelectionPreset = SplitSelectionPreset.CustomSelection;
        foreach (var page in Pages)
        {
            page.IsSelected = false;
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private bool CanRunOrganizerAction()
        => !IsBusy
           && !string.IsNullOrWhiteSpace(InputPath)
           && !string.IsNullOrWhiteSpace(OutputFolder)
           && CanAccessProtectedDocument()
           && SelectedPages.Count > 0;

    private async void ExtractSelectedPages()
    {
        await RunSplitOperationAsync(
            "Extracting selected pages...",
            35,
            () => _service.ExtractSelectedPages(CreateOperationOptions()),
            "Extract pages completed.",
            "Extract pages failed.");
    }

    private async void RemoveSelectedPages()
    {
        await RunSplitOperationAsync(
            "Removing selected pages...",
            40,
            () => _service.RemoveSelectedPages(CreateOperationOptions()),
            "Remove selected pages completed.",
            "Remove selected pages failed.");
    }

    private void RotateSelectedPages(int delta)
    {
        var selectedPages = SelectedPages;
        if (selectedPages.Count == 0)
        {
            return;
        }

        foreach (var page in selectedPages)
        {
            page.Rotation += delta;
        }

        LastOperationSucceeded = true;
        StatusMessage = delta > 0
            ? $"Rotated {selectedPages.Count} selected page(s) to the right."
            : $"Rotated {selectedPages.Count} selected page(s) to the left.";
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private async Task RunSplitOperationAsync(
        string startMessage,
        double progress,
        Func<OperationResult> operation,
        string successStatus,
        string failureStatus,
        bool shouldReloadPages = false)
    {
        IsBusy = true;
        _statusService.Start(startMessage);
        _statusService.Report("Processing pages...", progress);

        var result = await Task.Run(operation);

        IsBusy = false;
        LastOperationSucceeded = result.Success;
        StatusMessage = result.Message;

        if (result.Success)
        {
            _recentFilesService.AddFile(InputPath);
            _statusService.Complete(successStatus);

            if (shouldReloadPages)
            {
                LoadDocumentPages();
            }
        }
        else
        {
            _statusService.Fail(failureStatus);
        }

        ClearDocumentPasswords();
    }

    private PdfSplitOperationOptions CreateOperationOptions(int rotationDelta = 0)
        => new()
        {
            InputPath = InputPath,
            OutputFolder = OutputFolder,
            Password = GetEffectiveAccessPassword(),
            SelectedPages = SelectedPages.Select(page => page.PageNumber).ToList(),
            PageSequence = Pages.Select(page => page.PageNumber).ToList(),
            PageRotations = Pages.ToDictionary(page => page.PageNumber, page => page.Rotation),
            OutputStrategy = OutputStrategy,
            RotationDelta = rotationDelta
        };

    private void UpdateSelectionSummary()
    {
        var selectedPages = SelectedPages;
        if (Pages.Count == 0)
        {
            SelectionSummary = "No document loaded.";
            return;
        }

        SelectionSummary = selectedPages.Count == 0
            ? $"0 selected out of {Pages.Count} page(s)."
            : $"{selectedPages.Count} selected out of {Pages.Count} page(s): {FormatPageList(selectedPages.Select(page => page.PageNumber))}";
    }

    private void UpdateNamingPreview()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            OutputPreviewSummary = "Output summary will appear here.";
            OutputPreviewItems.Clear();
            return;
        }

        var baseName = Path.GetFileNameWithoutExtension(InputPath);
        var selectedPageNumbers = SelectedPages.Select(page => page.PageNumber).ToList();

        if (selectedPageNumbers.Count == 0)
        {
            OutputPreviewSummary = "0 page(s) selected.";
            OutputPreviewItems.Clear();
            return;
        }

        var previewItems = BuildOutputPreviewItems(baseName, selectedPageNumbers);

        OutputPreviewItems.Clear();
        foreach (var item in previewItems)
        {
            OutputPreviewItems.Add(item);
        }

        OutputPreviewSummary = $"{selectedPageNumbers.Count} page(s) selected. Estimated output files: {previewItems.Count}.";
    }

    private void UpdateValidationMessage()
    {
        if (!string.IsNullOrWhiteSpace(_documentLoadMessage))
        {
            ValidationMessage = _documentLoadMessage;
            return;
        }

        if (!CanAccessProtectedDocument())
        {
            ValidationMessage = _isDocumentPasswordIncorrect
                ? "Incorrect password. Enter the correct password and reload the document."
                : _requiresDocumentPassword
                    ? "Password required. Enter the password and reload the document."
                    : "Owner password is required to organize pages from this protected PDF.";
            return;
        }

        if (string.IsNullOrWhiteSpace(InputPath))
        {
            ValidationMessage = "Choose a PDF to begin.";
            return;
        }

        if (!FileAccessHelper.TryValidateReadableFile(InputPath, out var inputError))
        {
            ValidationMessage = inputError;
            return;
        }

        if (!FileAccessHelper.TryValidateOutputFolder(OutputFolder, out var outputError))
        {
            ValidationMessage = outputError;
            return;
        }

        if (IsPageListPreset && !TryParseSelectionInput(out _, out var selectionError))
        {
            ValidationMessage = selectionError;
            return;
        }

        if (!SelectedPages.Any())
        {
            ValidationMessage = "Select at least one page.";
            return;
        }

        ValidationMessage = "Ready to extract, remove, or rotate selected pages.";
    }

    private void PagesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<PdfPageOrganizerItem>())
            {
                item.PropertyChanged -= PageOnPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<PdfPageOrganizerItem>())
            {
                item.PropertyChanged += PageOnPropertyChanged;
            }
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private void PageOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PdfPageOrganizerItem.IsSelected))
        {
            return;
        }

        if (!_isApplyingSelectionPreset && SelectionPreset != SplitSelectionPreset.PageList)
        {
            SelectionPreset = SplitSelectionPreset.CustomSelection;
            return;
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    public void ReorderPages(IReadOnlyList<PdfPageOrganizerItem> draggedPages, PdfPageOrganizerItem? targetPage)
    {
        if (draggedPages.Count == 0)
        {
            return;
        }

        var orderedDraggedPages = Pages.Where(draggedPages.Contains).ToList();
        if (orderedDraggedPages.Count == 0)
        {
            return;
        }

        if (targetPage != null && orderedDraggedPages.Contains(targetPage))
        {
            return;
        }

        var targetIndex = targetPage == null ? Pages.Count : Pages.IndexOf(targetPage) + 1;
        if (targetIndex < 0)
        {
            targetIndex = Pages.Count;
        }

        foreach (var page in orderedDraggedPages)
        {
            var pageIndex = Pages.IndexOf(page);
            if (pageIndex >= 0 && pageIndex < targetIndex)
            {
                targetIndex--;
            }

            Pages.Remove(page);
        }

        for (var index = 0; index < orderedDraggedPages.Count; index++)
        {
            Pages.Insert(Math.Min(targetIndex + index, Pages.Count), orderedDraggedPages[index]);
        }

        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    public void ClearDropTargets()
    {
        foreach (var page in Pages)
        {
            page.IsDropTarget = false;
        }
    }

    public void SetDropTarget(PdfPageOrganizerItem? targetPage)
    {
        ClearDropTargets();
        if (targetPage != null)
        {
            targetPage.IsDropTarget = true;
        }
    }

    public void ClearTransientData()
    {
        InputPath = string.Empty;
        OutputFolder = string.Empty;
        DocumentPassword = string.Empty;
        PageSelectionInput = string.Empty;
        Pages.Clear();
        OutputPreviewItems.Clear();
        UpdateDocumentSecurityState(null);
        _documentLoadMessage = "Choose a PDF to begin.";
        _inputFileSizeBytes = 0;
        PageCountText = "0 pages";
        SelectionSummary = "No document loaded.";
        OutputPreviewSummary = "Output summary will appear here.";
        ValidationMessage = "Choose a PDF to begin.";
        LastOperationSucceeded = false;
        StatusMessage = "Page Organizer workspace cleared.";
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        ReloadProtectedDocumentCommand.RaiseCanExecuteChanged();
        ExtractSelectedCommand.RaiseCanExecuteChanged();
        RemoveSelectedPagesCommand.RaiseCanExecuteChanged();
        RotateLeftCommand.RaiseCanExecuteChanged();
        RotateRightCommand.RaiseCanExecuteChanged();
        SelectAllPagesCommand.RaiseCanExecuteChanged();
        ClearPageSelectionCommand.RaiseCanExecuteChanged();
        ApplySelectionPresetCommand.RaiseCanExecuteChanged();
    }

    private async void LoadThumbnailsAsync(string inputPath, string? password)
    {
        if (Pages.Count == 0)
        {
            return;
        }

        var results = await Task.Run(() => _thumbnailService.RenderDocumentThumbnails(inputPath, 84, 112, null, password));

        if (!string.Equals(InputPath, inputPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var result in results)
        {
            var page = Pages.FirstOrDefault(item => item.PageNumber == result.PageNumber);
            if (page != null)
            {
                page.Thumbnail = result.Thumbnail;
            }
        }
    }

    private bool IsPageNumberInSelectionInput(int pageNumber)
    {
        if (!TryParseSelectionInput(out var selectedPageNumbers, out _))
        {
            return false;
        }

        return selectedPageNumbers.Contains(pageNumber);
    }

    private bool TryParseSelectionInput(out HashSet<int> selectedPageNumbers, out string errorMessage)
    {
        selectedPageNumbers = new HashSet<int>();
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(PageSelectionInput))
        {
            errorMessage = "Enter pages like 1, 3-8, 15.";
            return false;
        }

        try
        {
            var parsed = PageRangeParser.Parse(PageSelectionInput, Pages.Count);
            selectedPageNumbers = parsed.SelectMany(range => range).ToHashSet();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private long EstimateOutputSizeBytes(int selectedPageCount)
    {
        if (_inputFileSizeBytes <= 0 || Pages.Count == 0 || selectedPageCount <= 0)
        {
            return 0;
        }

        var ratio = (double)selectedPageCount / Pages.Count;
        var estimatedBytes = _inputFileSizeBytes * ratio;

        if (OutputStrategy == SplitOutputStrategy.SeparateFiles)
        {
            estimatedBytes *= 1.12;
        }
        else if (OutputStrategy == SplitOutputStrategy.RangeFiles)
        {
            estimatedBytes *= 1.06;
        }

        return (long)Math.Max(estimatedBytes, 1024);
    }

    private List<SplitOutputPreviewItem> BuildOutputPreviewItems(string baseName, IReadOnlyList<int> selectedPageNumbers)
    {
        return OutputStrategy switch
        {
            SplitOutputStrategy.SeparateFiles => selectedPageNumbers
                .Take(12)
                .Select(page => new SplitOutputPreviewItem
                {
                    FileName = $"{baseName}_page_{page:000}.pdf",
                    PageCount = 1,
                    FileSizeText = FormatBytes(EstimateOutputSizeBytes(1))
                })
                .ToList(),
            SplitOutputStrategy.RangeFiles => BuildContiguousRanges(selectedPageNumbers)
                .Take(12)
                .Select(range => new SplitOutputPreviewItem
                {
                    FileName = range.Count == 1
                        ? $"{baseName}_p{range[0]}.pdf"
                        : $"{baseName}_p{range.First()}-p{range.Last()}.pdf",
                    PageCount = range.Count,
                    FileSizeText = FormatBytes(EstimateOutputSizeBytes(range.Count))
                })
                .ToList(),
            _ =>
                [
                    new SplitOutputPreviewItem
                    {
                        FileName = $"{baseName}_selected.pdf",
                        PageCount = selectedPageNumbers.Count,
                        FileSizeText = FormatBytes(EstimateOutputSizeBytes(selectedPageNumbers.Count))
                    }
                ]
        };
    }

    public SplitSessionState CaptureSessionState()
    {
        return new SplitSessionState
        {
            InputPath = InputPath,
            OutputFolder = OutputFolder,
            SelectionPreset = SelectionPreset,
            OutputStrategy = OutputStrategy,
            PageSelectionInput = PageSelectionInput,
            Pages = Pages.Select(page => new SplitPageSessionState
            {
                SourcePageNumber = page.SourcePageNumber,
                Rotation = page.Rotation,
                IsSelected = page.IsSelected
            }).ToList()
        };
    }

    public void RestoreSessionState(SplitSessionState? state)
    {
        if (state == null)
        {
            return;
        }

        InputPath = state.InputPath ?? string.Empty;
        OutputFolder = state.OutputFolder ?? string.Empty;
        ClearDocumentPasswords();
        OutputStrategy = state.OutputStrategy;
        SelectionPreset = state.SelectionPreset;
        PageSelectionInput = state.PageSelectionInput ?? string.Empty;

        if (Pages.Count > 0 && state.Pages.Count > 0)
        {
            var savedPages = state.Pages.ToDictionary(page => page.SourcePageNumber);
            var orderedPages = new List<PdfPageOrganizerItem>();

            foreach (var savedPage in state.Pages)
            {
                var page = Pages.FirstOrDefault(item => item.SourcePageNumber == savedPage.SourcePageNumber);
                if (page != null)
                {
                    orderedPages.Add(page);
                }
            }

            orderedPages.AddRange(Pages.Where(page => !savedPages.ContainsKey(page.SourcePageNumber)));

            _isApplyingSelectionPreset = true;
            try
            {
                Pages.Clear();
                for (var index = 0; index < orderedPages.Count; index++)
                {
                    var page = orderedPages[index];
                    if (savedPages.TryGetValue(page.SourcePageNumber, out var pageState))
                    {
                        page.Rotation = pageState.Rotation;
                        page.IsSelected = pageState.IsSelected;
                    }
                    else
                    {
                        page.Rotation = 0;
                        page.IsSelected = false;
                    }

                    page.PageNumber = index + 1;
                    Pages.Add(page);
                }
            }
            finally
            {
                _isApplyingSelectionPreset = false;
            }
        }

        LastOperationSucceeded = false;
        StatusMessage = "Page Organizer session restored. Passwords must be re-entered.";
        UpdateSelectionSummary();
        UpdateNamingPreview();
        UpdateValidationMessage();
        RefreshCommands();
    }

    private string GetEffectiveAccessPassword()
        => DocumentPassword;

    private bool CanAccessProtectedDocument()
        => !_requiresDocumentPassword && !_isDocumentPasswordIncorrect && (!_isDocumentEncrypted || _hasOwnerLevelAccess);

    private void UpdateDocumentSecurityState(PdfPageOrganizerDocumentInfo? info)
    {
        _isDocumentEncrypted = info?.IsEncrypted == true;
        _requiresDocumentPassword = info?.RequiresPassword == true;
        _isDocumentPasswordIncorrect = info?.IsPasswordIncorrect == true;
        _hasOwnerLevelAccess = info?.HasOwnerPermissions != false;

        DocumentEncryptionText = info == null
            ? "Not loaded"
            : !_isDocumentEncrypted
                ? "Not encrypted"
                : _hasOwnerLevelAccess
                    ? "Encrypted - owner access granted"
                    : "Encrypted - owner password required";

        DocumentPasswordHint = info == null
            ? "Load a PDF to check whether it needs a password."
            : _isDocumentPasswordIncorrect
                ? "Incorrect password. Enter the owner password and unlock again."
                : _requiresDocumentPassword
                    ? "This PDF is protected. Enter the owner password to unlock page organizing."
                    : _isDocumentEncrypted && !_hasOwnerLevelAccess
                        ? "Owner password required before you can extract, remove, or rotate pages."
                        : "This document is ready for page organizing.";

        OnPropertyChanged(nameof(IsDocumentPasswordPanelVisible));
    }

    private void ClearDocumentPasswords()
    {
        DocumentPassword = string.Empty;

        if (_isDocumentEncrypted)
        {
            UpdateDocumentSecurityState(new PdfPageOrganizerDocumentInfo
            {
                IsEncrypted = true,
                RequiresPassword = true,
                HasOwnerPermissions = false,
                StatusMessage = "Re-enter the owner password to continue organizing this protected PDF."
            });
            UpdateValidationMessage();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "n/a";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string FormatPageList(IEnumerable<int> pages)
        => string.Join(", ", BuildContiguousRanges(pages.OrderBy(page => page).ToList())
            .Select(range => range.Count == 1 ? range[0].ToString() : $"{range[0]}-{range[^1]}"));

    private static List<List<int>> BuildContiguousRanges(IReadOnlyList<int> sortedPages)
    {
        var ranges = new List<List<int>>();
        if (sortedPages.Count == 0)
        {
            return ranges;
        }

        var current = new List<int> { sortedPages[0] };
        for (var index = 1; index < sortedPages.Count; index++)
        {
            if (sortedPages[index] == sortedPages[index - 1] + 1)
            {
                current.Add(sortedPages[index]);
                continue;
            }

            ranges.Add(current);
            current = new List<int> { sortedPages[index] };
        }

        ranges.Add(current);
        return ranges;
    }
}
