using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Win32;
using PdfTool.App.Commands;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfTool.App.Services;
using WinForms = System.Windows.Forms;

namespace PdfTool.App.ViewModels;

public class ProtectViewModel : BaseViewModel
{
    private readonly IPdfProtectionService _service;
    private readonly IPdfDocumentInspectorService _inspectorService;
    private readonly IAppStatusService _statusService;
    private string _inputPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _userPassword = string.Empty;
    private string _ownerPassword = string.Empty;
    private string _statusMessage = "Choose a file or batch, then run the PDF security action.";
    private bool _lastOperationSucceeded;
    private bool _allowPrint = true;
    private bool _allowFullQualityPrint = true;
    private bool _allowModifyDocument = true;
    private bool _allowExtractContent = true;
    private bool _allowAnnotations = true;
    private bool _allowFormsFill = true;
    private bool _allowAssembleDocument = true;
    private bool _isBusy;
    private ProtectActionMode _actionMode = ProtectActionMode.Protect;
    private ProtectInputMode _inputMode = ProtectInputMode.SingleFile;
    private BatchPasswordStrategy _batchPasswordStrategy = BatchPasswordStrategy.OnePasswordForAll;
    private string _batchSourceFolder = string.Empty;
    private string _batchCommonPassword = string.Empty;
    private string _batchOutputFolder = string.Empty;
    private string _batchSummaryText = "No batch files loaded.";
    private string _batchValidationText = "Validation has not run yet.";
    private bool _isUserPasswordVisible;
    private bool _isOwnerPasswordVisible;
    private bool _isCommonPasswordVisible;
    private int _userPasswordStrengthScore;
    private string _userPasswordStrengthLabel = "Empty";
    private string _userPasswordStrengthGuidance = "Enter a password to protect the PDF.";
    private int _ownerPasswordStrengthScore;
    private string _ownerPasswordStrengthLabel = "Empty";
    private string _ownerPasswordStrengthGuidance = "Owner password is optional unless permissions are restricted.";
    private int _commonPasswordStrengthScore;
    private string _commonPasswordStrengthLabel = "Empty";
    private string _commonPasswordStrengthGuidance = "Use one shared password for all loaded files.";
    private ProtectBatchItem? _selectedBatchItem;
    private bool _suppressBatchValidationRefresh;

    public ProtectViewModel(
        IPdfProtectionService service,
        IPdfDocumentInspectorService inspectorService,
        IAppStatusService statusService)
    {
        _service = service;
        _inspectorService = inspectorService;
        _statusService = statusService;
        BatchItems = new ObservableCollection<ProtectBatchItem>();
        BatchItems.CollectionChanged += BatchItemsOnCollectionChanged;

        BrowseInputCommand = new RelayCommand(BrowseInput);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        BrowseMultipleFilesCommand = new RelayCommand(BrowseMultipleFiles, () => !IsBusy && !IsSingleMode);
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsBusy && !IsSingleMode);
        ProtectCommand = new RelayCommand(ProtectPdf, CanProtect);
        ApplySamePasswordCommand = new RelayCommand(ApplySamePasswordToAll, () => !IsBusy && BatchItems.Count > 0 && !string.IsNullOrWhiteSpace(BatchCommonPassword));
        GeneratePasswordsCommand = new RelayCommand(GeneratePasswords, () => !IsBusy && BatchItems.Count > 0 && IsProtectAction);
        ImportMappingCommand = new RelayCommand(ImportMapping, () => !IsBusy && BatchItems.Count > 0);
        ExportTemplateCommand = new RelayCommand(ExportTemplate, () => BatchItems.Count > 0);
        ExportBatchInfoCommand = new RelayCommand(ExportBatchInfo, () => BatchItems.Count > 0);
        RefreshBatchGridCommand = new RelayCommand(RefreshBatchGrid, () => BatchItems.Count > 0 && !IsBusy);
        ValidateBatchGridCommand = new RelayCommand(ValidateBatchGrid, () => BatchItems.Count > 0 && !IsBusy);
        ClearAllPasswordsCommand = new RelayCommand(ClearAllPasswords, () => !IsBusy && (HasAnyPasswordValues()));
        SelectBatchOutputFolderCommand = new RelayCommand(SelectBatchOutputFolder, () => !IsBusy && !IsSingleMode && BatchItems.Count > 0);
        ToggleBatchItemPasswordVisibilityCommand = new RelayCommand<ProtectBatchItem>(ToggleBatchItemPasswordVisibility);
        ToggleBatchItemOwnerPasswordVisibilityCommand = new RelayCommand<ProtectBatchItem>(ToggleBatchItemOwnerPasswordVisibility);
        AddBatchFilesCommand = new RelayCommand(AddBatchFiles, () => !IsBusy && !IsSingleMode);
        AddBatchFolderCommand = new RelayCommand(AddBatchFolder, () => !IsBusy && !IsSingleMode);
        RemoveSelectedBatchItemCommand = new RelayCommand(RemoveSelectedBatchItem, () => !IsBusy && SelectedBatchItem != null);

        UpdateUserPasswordStrength();
        UpdateOwnerPasswordStrength();
        UpdateCommonPasswordStrength();
    }

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (SetProperty(ref _inputPath, value))
            {
                if (string.IsNullOrWhiteSpace(OutputPath) && !string.IsNullOrWhiteSpace(value))
                {
                    OutputPath = CreateDefaultOutputPath(value);
                }

                RefreshCommands();
            }
        }
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

    public string UserPassword
    {
        get => _userPassword;
        set
        {
            if (SetProperty(ref _userPassword, value))
            {
                UpdateUserPasswordStrength();
                RefreshCommands();
            }
        }
    }

    public string OwnerPassword
    {
        get => _ownerPassword;
        set
        {
            if (SetProperty(ref _ownerPassword, value))
            {
                UpdateOwnerPasswordStrength();
                RefreshCommands();
            }
        }
    }

    public bool AllowPrint
    {
        get => _allowPrint;
        set
        {
            if (SetProperty(ref _allowPrint, value))
            {
                if (!value)
                {
                    AllowFullQualityPrint = false;
                }

                RefreshCommands();
            }
        }
    }

    public bool AllowFullQualityPrint
    {
        get => _allowFullQualityPrint;
        set => SetProperty(ref _allowFullQualityPrint, value);
    }

    public bool AllowModifyDocument
    {
        get => _allowModifyDocument;
        set
        {
            if (SetProperty(ref _allowModifyDocument, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool AllowExtractContent
    {
        get => _allowExtractContent;
        set
        {
            if (SetProperty(ref _allowExtractContent, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool AllowAnnotations
    {
        get => _allowAnnotations;
        set
        {
            if (SetProperty(ref _allowAnnotations, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool AllowFormsFill
    {
        get => _allowFormsFill;
        set
        {
            if (SetProperty(ref _allowFormsFill, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool AllowAssembleDocument
    {
        get => _allowAssembleDocument;
        set
        {
            if (SetProperty(ref _allowAssembleDocument, value))
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

    public ProtectActionMode ActionMode
    {
        get => _actionMode;
        set
        {
            if (SetProperty(ref _actionMode, value))
            {
                OnPropertyChanged(nameof(IsProtectAction));
                OnPropertyChanged(nameof(IsUnlockAction));
                OnPropertyChanged(nameof(PrimaryPasswordLabel));
                OnPropertyChanged(nameof(ExecuteActionLabel));
                OnPropertyChanged(nameof(BatchModeTitle));
                UpdateOutputPathsForActionMode();
                UpdateUserPasswordStrength();
                UpdateCommonPasswordStrength();
                RefreshCommands();
            }
        }
    }

    public bool IsProtectAction
    {
        get => ActionMode == ProtectActionMode.Protect;
        set
        {
            if (value)
            {
                ActionMode = ProtectActionMode.Protect;
            }
        }
    }

    public bool IsUnlockAction
    {
        get => ActionMode == ProtectActionMode.Unlock;
        set
        {
            if (value)
            {
                ActionMode = ProtectActionMode.Unlock;
            }
        }
    }

    public ProtectInputMode InputMode
    {
        get => _inputMode;
        set
        {
            var normalizedMode = Enum.IsDefined(typeof(ProtectInputMode), value)
                ? value
                : ProtectInputMode.MultipleFiles;

            if (SetProperty(ref _inputMode, normalizedMode))
            {
                OnPropertyChanged(nameof(IsSingleMode));
                OnPropertyChanged(nameof(IsMultipleMode));
                RefreshCommands();
            }
        }
    }

    public BatchPasswordStrategy BatchPasswordStrategy
    {
        get => _batchPasswordStrategy;
        set
        {
            if (SetProperty(ref _batchPasswordStrategy, value))
            {
                OnPropertyChanged(nameof(IsOnePasswordForAll));
                OnPropertyChanged(nameof(IsOnePasswordPerFile));
                OnPropertyChanged(nameof(IsImportMappingMode));
                RefreshCommands();
            }
        }
    }

    public bool IsSingleMode
    {
        get => InputMode == ProtectInputMode.SingleFile;
        set
        {
            if (value)
            {
                InputMode = ProtectInputMode.SingleFile;
            }
        }
    }

    public bool IsMultipleMode
    {
        get => InputMode == ProtectInputMode.MultipleFiles;
        set
        {
            if (value)
            {
                InputMode = ProtectInputMode.MultipleFiles;
            }
        }
    }

    public bool IsOnePasswordForAll
    {
        get => BatchPasswordStrategy == BatchPasswordStrategy.OnePasswordForAll;
        set
        {
            if (value)
            {
                BatchPasswordStrategy = BatchPasswordStrategy.OnePasswordForAll;
            }
        }
    }

    public bool IsOnePasswordPerFile
    {
        get => BatchPasswordStrategy == BatchPasswordStrategy.OnePasswordPerFile;
        set
        {
            if (value)
            {
                BatchPasswordStrategy = BatchPasswordStrategy.OnePasswordPerFile;
            }
        }
    }

    public bool IsImportMappingMode
    {
        get => BatchPasswordStrategy == BatchPasswordStrategy.ImportMapping;
        set
        {
            if (value)
            {
                BatchPasswordStrategy = BatchPasswordStrategy.ImportMapping;
            }
        }
    }

    public string PrimaryPasswordLabel => IsProtectAction ? "User password:" : "Owner password:";
    public string ExecuteActionLabel => IsProtectAction ? "Protect PDF" : "Unlock PDF";
    public string BatchModeTitle => "Input scope";

    public string BatchSourceFolder
    {
        get => _batchSourceFolder;
        set => SetProperty(ref _batchSourceFolder, value);
    }

    public string BatchCommonPassword
    {
        get => _batchCommonPassword;
        set
        {
            if (SetProperty(ref _batchCommonPassword, value))
            {
                UpdateCommonPasswordStrength();
                RefreshCommands();
            }
        }
    }

    public string BatchOutputFolder
    {
        get => _batchOutputFolder;
        set => SetProperty(ref _batchOutputFolder, value);
    }

    public string BatchSummaryText
    {
        get => _batchSummaryText;
        set => SetProperty(ref _batchSummaryText, value);
    }

    public string BatchValidationText
    {
        get => _batchValidationText;
        set => SetProperty(ref _batchValidationText, value);
    }

    public bool IsUserPasswordVisible
    {
        get => _isUserPasswordVisible;
        set => SetProperty(ref _isUserPasswordVisible, value);
    }

    public bool IsOwnerPasswordVisible
    {
        get => _isOwnerPasswordVisible;
        set => SetProperty(ref _isOwnerPasswordVisible, value);
    }

    public bool IsCommonPasswordVisible
    {
        get => _isCommonPasswordVisible;
        set => SetProperty(ref _isCommonPasswordVisible, value);
    }

    public int UserPasswordStrengthScore
    {
        get => _userPasswordStrengthScore;
        set => SetProperty(ref _userPasswordStrengthScore, value);
    }

    public string UserPasswordStrengthLabel
    {
        get => _userPasswordStrengthLabel;
        set => SetProperty(ref _userPasswordStrengthLabel, value);
    }

    public string UserPasswordStrengthGuidance
    {
        get => _userPasswordStrengthGuidance;
        set => SetProperty(ref _userPasswordStrengthGuidance, value);
    }

    public int OwnerPasswordStrengthScore
    {
        get => _ownerPasswordStrengthScore;
        set => SetProperty(ref _ownerPasswordStrengthScore, value);
    }

    public string OwnerPasswordStrengthLabel
    {
        get => _ownerPasswordStrengthLabel;
        set => SetProperty(ref _ownerPasswordStrengthLabel, value);
    }

    public string OwnerPasswordStrengthGuidance
    {
        get => _ownerPasswordStrengthGuidance;
        set => SetProperty(ref _ownerPasswordStrengthGuidance, value);
    }

    public int CommonPasswordStrengthScore
    {
        get => _commonPasswordStrengthScore;
        set => SetProperty(ref _commonPasswordStrengthScore, value);
    }

    public string CommonPasswordStrengthLabel
    {
        get => _commonPasswordStrengthLabel;
        set => SetProperty(ref _commonPasswordStrengthLabel, value);
    }

    public string CommonPasswordStrengthGuidance
    {
        get => _commonPasswordStrengthGuidance;
        set => SetProperty(ref _commonPasswordStrengthGuidance, value);
    }

    public ObservableCollection<ProtectBatchItem> BatchItems { get; }

    public ProtectBatchItem? SelectedBatchItem
    {
        get => _selectedBatchItem;
        set
        {
            if (SetProperty(ref _selectedBatchItem, value))
            {
                RemoveSelectedBatchItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand BrowseInputCommand { get; }
    public RelayCommand BrowseOutputCommand { get; }
    public RelayCommand BrowseMultipleFilesCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ProtectCommand { get; }
    public RelayCommand ApplySamePasswordCommand { get; }
    public RelayCommand GeneratePasswordsCommand { get; }
    public RelayCommand ImportMappingCommand { get; }
    public RelayCommand ExportTemplateCommand { get; }
    public RelayCommand ExportBatchInfoCommand { get; }
    public RelayCommand RefreshBatchGridCommand { get; }
    public RelayCommand ValidateBatchGridCommand { get; }
    public RelayCommand ClearAllPasswordsCommand { get; }
    public RelayCommand SelectBatchOutputFolderCommand { get; }
    public RelayCommand<ProtectBatchItem> ToggleBatchItemPasswordVisibilityCommand { get; }
    public RelayCommand<ProtectBatchItem> ToggleBatchItemOwnerPasswordVisibilityCommand { get; }
    public RelayCommand AddBatchFilesCommand { get; }
    public RelayCommand AddBatchFolderCommand { get; }
    public RelayCommand RemoveSelectedBatchItemCommand { get; }

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
            OutputPath = CreateDefaultOutputPath(InputPath);
            StatusMessage = "Input file selected.";
            LastOperationSucceeded = false;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = IsProtectAction ? "Save protected PDF" : "Save unlocked PDF",
            FileName = string.IsNullOrWhiteSpace(InputPath)
                ? (IsProtectAction ? "protected.pdf" : "unlocked.pdf")
                : Path.GetFileName(CreateDefaultOutputPath(InputPath))
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    private void BrowseMultipleFiles()
    {
        InputMode = ProtectInputMode.MultipleFiles;
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Select PDF files"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadBatchItems(dialog.FileNames, clearExisting: true);
            StatusMessage = IsProtectAction
                ? $"{BatchItems.Count} file(s) loaded for batch protection."
                : $"{BatchItems.Count} file(s) loaded for batch unlock.";
        }
    }

    private void BrowseFolder()
    {
        InputMode = ProtectInputMode.MultipleFiles;
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        BatchSourceFolder = dialog.SelectedPath;
        var files = Directory.GetFiles(BatchSourceFolder, "*.pdf", SearchOption.TopDirectoryOnly);
        LoadBatchItems(files, clearExisting: true);
        StatusMessage = $"{BatchItems.Count} PDF file(s) loaded from folder.";
    }

    private void LoadBatchItems(IEnumerable<string> filePaths, bool clearExisting)
    {
        if (clearExisting)
        {
            BatchItems.Clear();
        }

        foreach (var file in filePaths.Where(File.Exists))
        {
            if (BatchItems.Any(existing => string.Equals(existing.InputPath, file, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new ProtectBatchItem
            {
                InputPath = file,
                OutputPath = CreateDefaultOutputPath(file),
                OwnerPassword = IsProtectAction ? OwnerPassword : string.Empty,
                Status = "Ready"
            };

            BatchItems.Add(item);
        }

        UpdateBatchSummary();
        ValidateBatchGrid();
        ApplySamePasswordToAll();
    }

    private void AddBatchFiles()
    {
        InputMode = ProtectInputMode.MultipleFiles;
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Add PDF files to batch grid"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadBatchItems(dialog.FileNames, clearExisting: false);
        StatusMessage = $"{BatchItems.Count} file(s) currently in the batch grid.";
    }

    private void AddBatchFolder()
    {
        InputMode = ProtectInputMode.MultipleFiles;
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        BatchSourceFolder = dialog.SelectedPath;
        var beforeCount = BatchItems.Count;
        var files = Directory.GetFiles(BatchSourceFolder, "*.pdf", SearchOption.TopDirectoryOnly);
        LoadBatchItems(files, clearExisting: false);
        var addedCount = BatchItems.Count - beforeCount;
        StatusMessage = $"{addedCount} PDF file(s) added from folder. {BatchItems.Count} file(s) currently in the batch grid.";
    }

    private void RemoveSelectedBatchItem()
    {
        if (SelectedBatchItem == null)
        {
            return;
        }

        var removedFileName = SelectedBatchItem.FileName;
        BatchItems.Remove(SelectedBatchItem);
        SelectedBatchItem = null;

        UpdateBatchSummary();
        BatchValidationText = ValidateBatchGridCore().Message;
        StatusMessage = $"{removedFileName} removed from batch grid.";
    }

    private bool CanProtect()
    {
        if (IsBusy)
        {
            return false;
        }

        if (IsUnlockAction)
        {
            if (IsSingleMode)
            {
                return !string.IsNullOrWhiteSpace(InputPath)
                       && !string.IsNullOrWhiteSpace(OutputPath)
                       && !string.IsNullOrWhiteSpace(UserPassword);
            }

            return BatchItems.Count > 0 && BatchItems.All(item => !string.IsNullOrWhiteSpace(item.Password));
        }

        if (!string.IsNullOrWhiteSpace(OwnerPassword) || HasRestrictedPermissions())
        {
            if (string.IsNullOrWhiteSpace(OwnerPassword))
            {
                return false;
            }
        }

        if (IsSingleMode)
        {
            return !string.IsNullOrWhiteSpace(InputPath)
                   && !string.IsNullOrWhiteSpace(OutputPath)
                   && PasswordHelper.MeetsStrongPolicy(UserPassword)
                   && (string.IsNullOrWhiteSpace(OwnerPassword) || PasswordHelper.MeetsStrongPolicy(OwnerPassword));
        }

        return BatchItems.Count > 0
               && BatchItems.All(item => PasswordHelper.MeetsStrongPolicy(item.Password))
               && BatchItems.All(item =>
               {
                   var effectiveOwnerPassword = GetEffectiveBatchOwnerPassword(item);
                   if (HasRestrictedPermissions() && string.IsNullOrWhiteSpace(effectiveOwnerPassword))
                   {
                       return false;
                   }

                   return string.IsNullOrWhiteSpace(effectiveOwnerPassword) || PasswordHelper.MeetsStrongPolicy(effectiveOwnerPassword);
               });
    }

    private async void ProtectPdf()
    {
        if (IsSingleMode)
        {
            await ExecuteSingleAsync();
            return;
        }

        await ExecuteBatchAsync();
    }

    private async Task ExecuteSingleAsync()
    {
        IsBusy = true;
        _statusService.Start(IsProtectAction ? "Protecting PDF..." : "Unlocking PDF...");
        _statusService.Report(IsProtectAction ? "Applying security settings..." : "Removing PDF protection...", 40);

        OperationResult result;
        if (IsProtectAction)
        {
            result = await Task.Run(() => _service.Protect(new PdfProtectionOptions
            {
                InputPath = InputPath,
                OutputPath = OutputPath,
                UserPassword = UserPassword,
                OwnerPassword = OwnerPassword,
                AllowPrint = AllowPrint,
                AllowFullQualityPrint = AllowFullQualityPrint,
                AllowModifyDocument = AllowModifyDocument,
                AllowExtractContent = AllowExtractContent,
                AllowAnnotations = AllowAnnotations,
                AllowFormsFill = AllowFormsFill,
                AllowAssembleDocument = AllowAssembleDocument
            }));
        }
        else
        {
            result = await Task.Run(() => _service.Unlock(new PdfUnlockOptions
            {
                InputPath = InputPath,
                OutputPath = OutputPath,
                Password = UserPassword
            }));
        }

        IsBusy = false;
        LastOperationSucceeded = result.Success;
        StatusMessage = result.Message;

        if (result.Success)
        {
            _statusService.Complete(IsProtectAction ? "Protect PDF completed." : "Unlock PDF completed.");
        }
        else
        {
            _statusService.Fail(IsProtectAction ? "Protect PDF failed." : "Unlock PDF failed.");
        }

        ClearSensitiveInputs("Passwords cleared after execution.");
    }

    private async Task ExecuteBatchAsync()
    {
        IsBusy = true;
        LastOperationSucceeded = false;
        _statusService.Start(IsProtectAction ? "Protecting batch files..." : "Unlocking batch files...", isIndeterminate: false, progressValue: 0);

        var validation = ValidateBatchGridCore();
        BatchValidationText = validation.Message;

        if (!validation.IsValid)
        {
            IsBusy = false;
            StatusMessage = "Batch validation failed. Fix invalid rows before running.";
            _statusService.Fail("Batch validation failed.");
            return;
        }

        var successCount = 0;
        var failCount = 0;

        for (var index = 0; index < BatchItems.Count; index++)
        {
            var item = BatchItems[index];
            item.Status = "Processing...";

            _statusService.Report(
                $"{(IsProtectAction ? "Protecting" : "Unlocking")} {item.FileName} ({index + 1}/{BatchItems.Count})",
                (index * 100.0) / BatchItems.Count);

            OperationResult result;
            if (IsProtectAction)
            {
                var options = new PdfProtectionOptions
                {
                    InputPath = item.InputPath,
                    OutputPath = item.OutputPath,
                    UserPassword = item.Password,
                    OwnerPassword = GetEffectiveBatchOwnerPassword(item),
                    AllowPrint = AllowPrint,
                    AllowFullQualityPrint = AllowFullQualityPrint,
                    AllowModifyDocument = AllowModifyDocument,
                    AllowExtractContent = AllowExtractContent,
                    AllowAnnotations = AllowAnnotations,
                    AllowFormsFill = AllowFormsFill,
                    AllowAssembleDocument = AllowAssembleDocument
                };

                result = await Task.Run(() => _service.Protect(options));
            }
            else
            {
                var options = new PdfUnlockOptions
                {
                    InputPath = item.InputPath,
                    OutputPath = item.OutputPath,
                    Password = item.Password
                };

                result = await Task.Run(() => _service.Unlock(options));
            }

            if (result.Success)
            {
                item.Status = "Success";
                successCount++;
            }
            else
            {
                item.Status = result.Message.StartsWith("Error:", StringComparison.Ordinal)
                    ? result.Message
                    : $"Error: {result.Message}";
                failCount++;
            }
        }

        IsBusy = false;
        LastOperationSucceeded = failCount == 0;
        StatusMessage = $"Batch finished. Success: {successCount}. Failed: {failCount}.";
        UpdateBatchSummary();
        BatchValidationText = ValidateBatchGridCore().Message;

        if (failCount == 0)
        {
            _statusService.Complete(IsProtectAction ? "Batch protect completed." : "Batch unlock completed.");
        }
        else
        {
            _statusService.Fail(IsProtectAction ? "Batch protect finished with some failures." : "Batch unlock finished with some failures.");
        }

        ClearSensitiveInputs("Passwords cleared after execution. Re-enter passwords to run again.");
    }

    private void ApplySamePasswordToAll()
    {
        if (string.IsNullOrWhiteSpace(BatchCommonPassword) && (IsUnlockAction || string.IsNullOrWhiteSpace(OwnerPassword)))
        {
            return;
        }

        foreach (var item in BatchItems)
        {
            if (!string.IsNullOrWhiteSpace(BatchCommonPassword))
            {
                item.Password = BatchCommonPassword;
            }

            if (IsProtectAction && !string.IsNullOrWhiteSpace(OwnerPassword))
            {
                item.OwnerPassword = OwnerPassword;
            }

            item.Status = "Ready";
        }

        UpdateBatchSummary();
        ValidateBatchGrid();
    }

    private void GeneratePasswords()
    {
        if (IsUnlockAction)
        {
            return;
        }

        foreach (var item in BatchItems)
        {
            item.Password = PasswordHelper.GeneratePassphrase();
            item.Status = "Ready";
        }

        BatchPasswordStrategy = BatchPasswordStrategy.OnePasswordPerFile;
        StatusMessage = "Passphrases generated for all batch items.";
        UpdateBatchSummary();
        ValidateBatchGrid();
    }

    private void ImportMapping()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Mapping files (*.csv;*.xlsx)|*.csv;*.xlsx",
            Title = "Import password mapping"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var extension = Path.GetExtension(dialog.FileName);
        var mappings = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? LoadMappingsFromExcel(dialog.FileName)
            : LoadMappingsFromCsv(dialog.FileName);

        var applied = 0;

        foreach (var item in BatchItems)
        {
            var match = mappings.FirstOrDefault(mapping =>
                string.Equals(mapping.InputPath, item.InputPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mapping.FileName, item.FileName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(match.Password))
            {
                item.Password = match.Password;
            }

            if (!string.IsNullOrWhiteSpace(match.OwnerPassword))
            {
                item.OwnerPassword = match.OwnerPassword;
            }

            if (!string.IsNullOrWhiteSpace(match.OutputPath))
            {
                item.OutputPath = match.OutputPath;
            }

            item.Status = "Mapped";
            applied++;
        }

        BatchPasswordStrategy = BatchPasswordStrategy.ImportMapping;
        StatusMessage = $"Imported mapping for {applied} file(s).";
        UpdateBatchSummary();
        ValidateBatchGrid();
    }

    private void ExportTemplate()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Export password template",
            FileName = "protect-template.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("FileName,InputPath,OutputPath,Password,OwnerPassword");

        foreach (var item in BatchItems)
        {
            builder.AppendLine(string.Join(",",
                Csv(item.FileName),
                Csv(item.InputPath),
                Csv(item.OutputPath),
                Csv(item.Password),
                Csv(item.OwnerPassword)));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        StatusMessage = $"Template exported to {dialog.FileName}.";
    }

    private void ExportBatchInfo()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            Title = "Export batch file info",
            FileName = "protect-batch-info.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("FileName,InputPath,OutputPath,Password,OwnerPassword");

        foreach (var item in BatchItems)
        {
            builder.AppendLine(string.Join(",",
                Csv(item.FileName),
                Csv(item.InputPath),
                Csv(item.OutputPath),
                Csv(item.Password),
                Csv(item.OwnerPassword)));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        StatusMessage = $"Batch info exported to {dialog.FileName}.";
    }

    private void UpdateBatchSummary()
    {
        var missingPasswords = BatchItems.Count(item => string.IsNullOrWhiteSpace(item.Password));
        var missingOwnerPasswords = IsProtectAction && HasRestrictedPermissions()
            ? BatchItems.Count(item => string.IsNullOrWhiteSpace(GetEffectiveBatchOwnerPassword(item)))
            : 0;
        var warnings = BatchItems.Count(item => item.HasValidationWarning);
        var errors = BatchItems.Count(item => item.HasValidationError);
        BatchSummaryText = BatchItems.Count == 0
            ? "No batch files loaded."
            : $"{BatchItems.Count} files loaded. Missing passwords: {missingPasswords}. Missing owner passwords: {missingOwnerPasswords}. Warnings: {warnings}. Errors: {errors}.";
    }

    public void ClearAllPasswords()
    {
        ClearSensitiveInputs("Passwords cleared.", updateStatusMessage: true);
    }

    public void ToggleUserPasswordVisibility() => IsUserPasswordVisible = !IsUserPasswordVisible;
    public void ToggleOwnerPasswordVisibility() => IsOwnerPasswordVisible = !IsOwnerPasswordVisible;
    public void ToggleCommonPasswordVisibility() => IsCommonPasswordVisible = !IsCommonPasswordVisible;

    private bool HasRestrictedPermissions()
        => !AllowPrint
           || !AllowFullQualityPrint
           || !AllowModifyDocument
           || !AllowExtractContent
           || !AllowAnnotations
           || !AllowFormsFill
           || !AllowAssembleDocument;

    private void RefreshCommands()
    {
        BrowseMultipleFilesCommand.RaiseCanExecuteChanged();
        BrowseFolderCommand.RaiseCanExecuteChanged();
        ProtectCommand.RaiseCanExecuteChanged();
        ApplySamePasswordCommand.RaiseCanExecuteChanged();
        GeneratePasswordsCommand.RaiseCanExecuteChanged();
        ImportMappingCommand.RaiseCanExecuteChanged();
        ExportTemplateCommand.RaiseCanExecuteChanged();
        ExportBatchInfoCommand.RaiseCanExecuteChanged();
        RefreshBatchGridCommand.RaiseCanExecuteChanged();
        ValidateBatchGridCommand.RaiseCanExecuteChanged();
        ClearAllPasswordsCommand.RaiseCanExecuteChanged();
        SelectBatchOutputFolderCommand.RaiseCanExecuteChanged();
        AddBatchFilesCommand.RaiseCanExecuteChanged();
        AddBatchFolderCommand.RaiseCanExecuteChanged();
        RemoveSelectedBatchItemCommand.RaiseCanExecuteChanged();
    }

    private void BatchItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<ProtectBatchItem>())
            {
                item.PropertyChanged -= BatchItemOnPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<ProtectBatchItem>())
            {
                item.PropertyChanged += BatchItemOnPropertyChanged;
            }
        }

        UpdateBatchSummary();
        if (SelectedBatchItem != null && !BatchItems.Contains(SelectedBatchItem))
        {
            SelectedBatchItem = null;
        }
        RefreshCommands();
    }

    private void BatchItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProtectBatchItem.Password) or nameof(ProtectBatchItem.OwnerPassword) or nameof(ProtectBatchItem.OutputPath) or nameof(ProtectBatchItem.Status))
        {
            UpdateBatchSummary();
            RefreshCommands();
        }

        if (!_suppressBatchValidationRefresh &&
            e.PropertyName is nameof(ProtectBatchItem.Password) or nameof(ProtectBatchItem.OwnerPassword) or nameof(ProtectBatchItem.OutputPath) or nameof(ProtectBatchItem.InputPath))
        {
            BatchValidationText = ValidateBatchGridCore().Message;
        }
    }

    private void ClearSensitiveInputs(string validationMessage, bool updateStatusMessage = false)
    {
        _suppressBatchValidationRefresh = true;

        try
        {
            UserPassword = string.Empty;
            OwnerPassword = string.Empty;
            BatchCommonPassword = string.Empty;

            foreach (var item in BatchItems)
            {
                item.Password = string.Empty;
                item.OwnerPassword = string.Empty;
            }
        }
        finally
        {
            _suppressBatchValidationRefresh = false;
        }

        UpdateBatchSummary();
        BatchValidationText = validationMessage;
        if (updateStatusMessage)
        {
            StatusMessage = validationMessage;
        }
        RefreshCommands();
    }

    private void RefreshBatchGrid()
    {
        BatchItems.Clear();
        SelectedBatchItem = null;
        BatchSourceFolder = string.Empty;
        BatchOutputFolder = string.Empty;
        UpdateBatchSummary();
        BatchValidationText = "Validation has not run yet.";
        StatusMessage = "Batch grid cleared.";
    }

    public void ClearTransientData()
    {
        InputPath = string.Empty;
        OutputPath = string.Empty;
        UserPassword = string.Empty;
        OwnerPassword = string.Empty;
        BatchCommonPassword = string.Empty;
        BatchSourceFolder = string.Empty;
        BatchOutputFolder = string.Empty;
        SelectedBatchItem = null;
        BatchItems.Clear();
        BatchSummaryText = "No batch files loaded.";
        BatchValidationText = "Validation has not run yet.";
        LastOperationSucceeded = false;
        StatusMessage = "Protect workspace cleared.";
        RefreshCommands();
    }

    private void SelectBatchOutputFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        BatchOutputFolder = dialog.SelectedPath;

        foreach (var item in BatchItems)
        {
            var fileName = Path.GetFileNameWithoutExtension(item.InputPath);
            item.OutputPath = Path.Combine(BatchOutputFolder, IsProtectAction ? $"{fileName}.protected.pdf" : $"{fileName}.unlocked.pdf");
        }

        ValidateBatchGrid();
        StatusMessage = $"Batch output folder set to {BatchOutputFolder}.";
    }

    private void ValidateBatchGrid()
    {
        var result = ValidateBatchGridCore();
        BatchValidationText = result.Message;
        StatusMessage = result.Message;
    }

    private BatchValidationResult ValidateBatchGridCore()
    {
        if (IsUnlockAction)
        {
            return ValidateUnlockBatchGridCore();
        }

        var ready = 0;
        var errors = 0;
        var warnings = 0;
        var missingInput = 0;
        var missingOutput = 0;
        var missingPassword = 0;
        var locked = 0;
        var outputMatchesInput = 0;
        var outputLocked = 0;
        var shortPasswords = 0;
        var policyViolations = 0;
        var missingOwnerPasswords = 0;
        var ownerPolicyViolations = 0;
        var alreadyProtected = 0;
        var invalidPdf = 0;
        var protectedInputs = 0;

        foreach (var item in BatchItems)
        {
            var itemErrors = new List<string>();
            var itemWarnings = new List<string>();

            if (!File.Exists(item.InputPath))
            {
                itemErrors.Add("Input file missing");
                missingInput++;
            }

            if (string.IsNullOrWhiteSpace(item.OutputPath))
            {
                itemErrors.Add("Missing output path");
                missingOutput++;
            }

            if (string.IsNullOrWhiteSpace(item.Password))
            {
                itemErrors.Add("Missing password");
                missingPassword++;
            }

            if (!itemErrors.Any() &&
                string.Equals(item.InputPath, item.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                itemErrors.Add("Output matches input");
                outputMatchesInput++;
            }

            if (!itemErrors.Any() && !FileAccessHelper.TryValidateReadableFile(item.InputPath, out var readableError))
            {
                itemErrors.Add(readableError);
                locked++;
            }

            if (!itemErrors.Any() && !FileAccessHelper.TryValidateOutputFile(item.OutputPath, out var outputError))
            {
                itemErrors.Add(outputError);
                outputLocked++;
            }

            if (!string.IsNullOrWhiteSpace(item.Password) && PasswordHelper.IsTooShort(item.Password))
            {
                itemErrors.Add($"Password is shorter than {PasswordHelper.MinimumPasswordLength} characters");
                shortPasswords++;
            }

            if (!string.IsNullOrWhiteSpace(item.Password) && !PasswordHelper.MeetsStrongPolicy(item.Password))
            {
                itemErrors.Add("Password must include uppercase, lowercase, number, and special character");
                policyViolations++;
            }

            var effectiveOwnerPassword = GetEffectiveBatchOwnerPassword(item);
            if (HasRestrictedPermissions() && string.IsNullOrWhiteSpace(effectiveOwnerPassword))
            {
                itemErrors.Add("Owner password is required when permissions are restricted");
                missingOwnerPasswords++;
            }

            if (!string.IsNullOrWhiteSpace(effectiveOwnerPassword) && !PasswordHelper.MeetsStrongPolicy(effectiveOwnerPassword))
            {
                itemErrors.Add("Owner password must include uppercase, lowercase, number, and special character");
                ownerPolicyViolations++;
            }

            if (!itemErrors.Any())
            {
                var info = _inspectorService.Inspect(item.InputPath);
                if (!info.IsPdf)
                {
                    itemErrors.Add(info.StatusMessage);
                    invalidPdf++;
                }
                else if (info.RequiresPassword || info.IsPasswordIncorrect)
                {
                    itemErrors.Add("Input file is already protected and must be unlocked before batch protect.");
                    protectedInputs++;
                }
                else if (info.IsEncrypted)
                {
                    itemWarnings.Add("File already appears to be protected");
                    alreadyProtected++;
                }
            }

            if (itemErrors.Any())
            {
                item.Status = $"Error: {string.Join("; ", itemErrors)}";
                errors++;
                continue;
            }

            if (itemWarnings.Any())
            {
                item.Status = $"Warning: {string.Join("; ", itemWarnings)}";
                warnings++;
                continue;
            }

            item.Status = "Ready";
            ready++;
        }

        var isValid = errors == 0 && BatchItems.Count > 0;
        var message = BatchItems.Count == 0
            ? "No batch files loaded."
            : $"{ready} ready, {warnings} warning, {errors} error. Missing input: {missingInput}. Missing output: {missingOutput}. Missing password: {missingPassword}. Missing owner password: {missingOwnerPasswords}. Short password: {shortPasswords}. Policy violations: {policyViolations}. Owner policy violations: {ownerPolicyViolations}. Output matches input: {outputMatchesInput}. Input locked: {locked}. Output locked: {outputLocked}. Invalid PDF: {invalidPdf}. Protected input: {protectedInputs}. Already protected: {alreadyProtected}.";

        return new BatchValidationResult
        {
            IsValid = isValid,
            Message = message
        };
    }

    private BatchValidationResult ValidateUnlockBatchGridCore()
    {
        var ready = 0;
        var errors = 0;
        var warnings = 0;
        var missingInput = 0;
        var missingOutput = 0;
        var missingPassword = 0;
        var locked = 0;
        var outputMatchesInput = 0;
        var outputLocked = 0;
        var invalidPdf = 0;
        var incorrectPasswords = 0;
        var ownerPasswordRequired = 0;
        var unprotectedInputs = 0;

        foreach (var item in BatchItems)
        {
            var itemErrors = new List<string>();
            var itemWarnings = new List<string>();

            if (!File.Exists(item.InputPath))
            {
                itemErrors.Add("Input file missing");
                missingInput++;
            }

            if (string.IsNullOrWhiteSpace(item.OutputPath))
            {
                itemErrors.Add("Missing output path");
                missingOutput++;
            }

            if (string.IsNullOrWhiteSpace(item.Password))
            {
                itemErrors.Add("Missing current password");
                missingPassword++;
            }

            if (!itemErrors.Any() &&
                string.Equals(item.InputPath, item.OutputPath, StringComparison.OrdinalIgnoreCase))
            {
                itemErrors.Add("Output matches input");
                outputMatchesInput++;
            }

            if (!itemErrors.Any() && !FileAccessHelper.TryValidateReadableFile(item.InputPath, out var readableError))
            {
                itemErrors.Add(readableError);
                locked++;
            }

            if (!itemErrors.Any() && !FileAccessHelper.TryValidateOutputFile(item.OutputPath, out var outputError))
            {
                itemErrors.Add(outputError);
                outputLocked++;
            }

            if (!itemErrors.Any())
            {
                var plainInfo = _inspectorService.Inspect(item.InputPath);
                if (!plainInfo.IsPdf)
                {
                    itemErrors.Add(plainInfo.StatusMessage);
                    invalidPdf++;
                }
                else if (!plainInfo.IsEncrypted && !plainInfo.RequiresPassword)
                {
                    itemErrors.Add("Input file is not protected.");
                    unprotectedInputs++;
                }
                else
                {
                    var unlockedInfo = _inspectorService.Inspect(item.InputPath, item.Password);
                    if (!unlockedInfo.CanReadContents || unlockedInfo.IsPasswordIncorrect)
                    {
                        itemErrors.Add("Incorrect password for this PDF.");
                        incorrectPasswords++;
                    }
                    else if (!PdfSecurityAccessHelper.TryHasOwnerLevelAccess(item.InputPath, item.Password, out _))
                    {
                        itemErrors.Add("Owner password is required to unlock this PDF.");
                        ownerPasswordRequired++;
                    }
                    else if (unlockedInfo.IsEncrypted)
                    {
                        itemWarnings.Add("Output will remove protection and create an unlocked copy");
                    }
                }
            }

            if (itemErrors.Any())
            {
                item.Status = $"Error: {string.Join("; ", itemErrors)}";
                errors++;
                continue;
            }

            if (itemWarnings.Any())
            {
                item.Status = $"Warning: {string.Join("; ", itemWarnings)}";
                warnings++;
                continue;
            }

            item.Status = "Ready";
            ready++;
        }

        var isValid = errors == 0 && BatchItems.Count > 0;
        var message = BatchItems.Count == 0
            ? "No batch files loaded."
            : $"{ready} ready, {warnings} warning, {errors} error. Missing input: {missingInput}. Missing output: {missingOutput}. Missing password: {missingPassword}. Output matches input: {outputMatchesInput}. Input locked: {locked}. Output locked: {outputLocked}. Invalid PDF: {invalidPdf}. Incorrect password: {incorrectPasswords}. Owner password required: {ownerPasswordRequired}. Not protected: {unprotectedInputs}.";

        return new BatchValidationResult
        {
            IsValid = isValid,
            Message = message
        };
    }

    private void ToggleBatchItemPasswordVisibility(ProtectBatchItem? item)
    {
        if (item == null)
        {
            return;
        }

        item.IsPasswordVisible = !item.IsPasswordVisible;
    }

    private void ToggleBatchItemOwnerPasswordVisibility(ProtectBatchItem? item)
    {
        if (item == null)
        {
            return;
        }

        item.IsOwnerPasswordVisible = !item.IsOwnerPasswordVisible;
    }

    private static string Csv(string value)
        => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static List<MappingRow> LoadMappingsFromCsv(string path)
    {
        var rows = new List<MappingRow>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            rows.Add(new MappingRow
            {
                FileName = values.ElementAtOrDefault(0) ?? string.Empty,
                InputPath = values.ElementAtOrDefault(1) ?? string.Empty,
                OutputPath = values.ElementAtOrDefault(2) ?? string.Empty,
                Password = values.ElementAtOrDefault(3) ?? string.Empty,
                OwnerPassword = values.ElementAtOrDefault(4) ?? string.Empty
            });
        }

        return rows;
    }

    private static List<MappingRow> LoadMappingsFromExcel(string path)
    {
        var rows = new List<MappingRow>();
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheet(1);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            rows.Add(new MappingRow
            {
                FileName = row.Cell(1).GetString(),
                InputPath = row.Cell(2).GetString(),
                OutputPath = row.Cell(3).GetString(),
                Password = row.Cell(4).GetString(),
                OwnerPassword = row.Cell(5).GetString()
            });
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];

            if (current == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (current == ',' && !inQuotes)
            {
                result.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(current);
        }

        result.Add(builder.ToString());
        return result;
    }

    private sealed class MappingRow
    {
        public string FileName { get; set; } = string.Empty;
        public string InputPath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string OwnerPassword { get; set; } = string.Empty;
    }

    private bool HasAnyPasswordValues()
        => !string.IsNullOrWhiteSpace(UserPassword)
           || !string.IsNullOrWhiteSpace(OwnerPassword)
           || !string.IsNullOrWhiteSpace(BatchCommonPassword)
           || BatchItems.Any(item => !string.IsNullOrWhiteSpace(item.Password) || !string.IsNullOrWhiteSpace(item.OwnerPassword));

    private string GetEffectiveBatchOwnerPassword(ProtectBatchItem item)
        => !IsProtectAction
            ? string.Empty
            : !string.IsNullOrWhiteSpace(item.OwnerPassword)
                ? item.OwnerPassword
                : OwnerPassword;

    public ProtectSessionState CaptureSessionState()
    {
        return new ProtectSessionState
        {
            ActionMode = ActionMode,
            InputMode = InputMode,
            BatchPasswordStrategy = BatchPasswordStrategy,
            InputPath = InputPath,
            OutputPath = OutputPath,
            BatchSourceFolder = BatchSourceFolder,
            BatchOutputFolder = BatchOutputFolder,
            AllowPrint = AllowPrint,
            AllowFullQualityPrint = AllowFullQualityPrint,
            AllowModifyDocument = AllowModifyDocument,
            AllowExtractContent = AllowExtractContent,
            AllowAnnotations = AllowAnnotations,
            AllowFormsFill = AllowFormsFill,
            AllowAssembleDocument = AllowAssembleDocument,
            SelectedBatchItemIndex = SelectedBatchItem != null ? BatchItems.IndexOf(SelectedBatchItem) : -1,
            BatchItems = BatchItems.Select(item => new ProtectBatchItemSessionState
            {
                InputPath = item.InputPath,
                OutputPath = item.OutputPath,
                Status = item.Status
            }).ToList()
        };
    }

    public void RestoreSessionState(ProtectSessionState? state)
    {
        if (state == null)
        {
            return;
        }

        ActionMode = state.ActionMode;
        InputMode = state.InputMode;
        BatchPasswordStrategy = state.BatchPasswordStrategy;
        AllowPrint = state.AllowPrint;
        AllowFullQualityPrint = state.AllowFullQualityPrint;
        AllowModifyDocument = state.AllowModifyDocument;
        AllowExtractContent = state.AllowExtractContent;
        AllowAnnotations = state.AllowAnnotations;
        AllowFormsFill = state.AllowFormsFill;
        AllowAssembleDocument = state.AllowAssembleDocument;

        InputPath = state.InputPath ?? string.Empty;
        OutputPath = state.OutputPath ?? string.Empty;
        UserPassword = string.Empty;
        OwnerPassword = string.Empty;
        BatchCommonPassword = string.Empty;
        BatchSourceFolder = state.BatchSourceFolder ?? string.Empty;
        BatchOutputFolder = state.BatchOutputFolder ?? string.Empty;

        BatchItems.Clear();
        foreach (var itemState in state.BatchItems)
        {
            BatchItems.Add(new ProtectBatchItem
            {
                InputPath = itemState.InputPath,
                OutputPath = itemState.OutputPath,
                Status = string.IsNullOrWhiteSpace(itemState.Status) ? "Ready" : itemState.Status
            });
        }

        SelectedBatchItem = state.SelectedBatchItemIndex >= 0 && state.SelectedBatchItemIndex < BatchItems.Count
            ? BatchItems[state.SelectedBatchItemIndex]
            : null;

        UpdateBatchSummary();
        BatchValidationText = ValidateBatchGridCore().Message;
        LastOperationSucceeded = false;
        StatusMessage = "Protect session restored. Passwords must be re-entered.";
        RefreshCommands();
    }

    private string CreateDefaultOutputPath(string inputPath)
        => IsProtectAction
            ? FileNameHelper.CreateProtectedFilePath(inputPath)
            : FileNameHelper.CreateUnlockedFilePath(inputPath);

    private void UpdateOutputPathsForActionMode()
    {
        if (!string.IsNullOrWhiteSpace(InputPath))
        {
            OutputPath = CreateDefaultOutputPath(InputPath);
        }

        foreach (var item in BatchItems)
        {
            item.OutputPath = CreateDefaultOutputPath(item.InputPath);
        }

        BatchValidationText = ValidateBatchGridCore().Message;
        UpdateBatchSummary();
    }

    private void UpdateUserPasswordStrength()
    {
        var strength = PasswordHelper.EvaluateStrength(UserPassword);
        UserPasswordStrengthScore = strength.Score;
        UserPasswordStrengthLabel = strength.Label;
        UserPasswordStrengthGuidance = string.IsNullOrWhiteSpace(UserPassword)
            ? IsProtectAction
                ? "Enter a password to protect the PDF."
                : "Enter the owner password to create an unlocked copy."
            : IsProtectAction
                ? strength.Guidance
                : "This must be the owner or full-access password for the PDF.";
    }

    private void UpdateOwnerPasswordStrength()
    {
        var strength = PasswordHelper.EvaluateStrength(OwnerPassword);
        OwnerPasswordStrengthScore = strength.Score;
        OwnerPasswordStrengthLabel = strength.Label;
        OwnerPasswordStrengthGuidance = string.IsNullOrWhiteSpace(OwnerPassword)
            ? "Owner password is optional unless permissions are restricted."
            : strength.Guidance;
    }

    private void UpdateCommonPasswordStrength()
    {
        var strength = PasswordHelper.EvaluateStrength(BatchCommonPassword);
        CommonPasswordStrengthScore = strength.Score;
        CommonPasswordStrengthLabel = strength.Label;
        CommonPasswordStrengthGuidance = string.IsNullOrWhiteSpace(BatchCommonPassword)
            ? IsProtectAction
                ? "Use one shared password for all loaded files."
                : "Use one shared owner password for all loaded files."
            : IsProtectAction
                ? strength.Guidance
                : "This owner password will be used to unlock every loaded PDF.";
    }

    private sealed class BatchValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
