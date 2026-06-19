using System.IO;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Models;

public class CompressBatchItem : BaseViewModel
{
    private string _inputPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _status = "Pending";
    private string _originalSizeText = "-";
    private string _resultSizeText = "-";
    private bool _isFailed;
    private PdfCompressionRunSummary? _lastRunSummary;
    private string _password = string.Empty;
    private string _ownerPassword = string.Empty;
    private bool _isEncrypted;
    private bool _requiresPassword;
    private bool _isPasswordIncorrect;
    private bool _hasOwnerPermissions = true;
    private string _accessMessage = string.Empty;

    public string FileName => Path.GetFileName(InputPath);

    public string InputPath
    {
        get => _inputPath;
        set
        {
            if (SetProperty(ref _inputPath, value))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string OriginalSizeText
    {
        get => _originalSizeText;
        set => SetProperty(ref _originalSizeText, value);
    }

    public string ResultSizeText
    {
        get => _resultSizeText;
        set => SetProperty(ref _resultSizeText, value);
    }

    public bool IsFailed
    {
        get => _isFailed;
        set => SetProperty(ref _isFailed, value);
    }

    public PdfCompressionRunSummary? LastRunSummary
    {
        get => _lastRunSummary;
        set => SetProperty(ref _lastRunSummary, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string OwnerPassword
    {
        get => _ownerPassword;
        set => SetProperty(ref _ownerPassword, value);
    }

    public bool IsEncrypted
    {
        get => _isEncrypted;
        set => SetProperty(ref _isEncrypted, value);
    }

    public bool RequiresPassword
    {
        get => _requiresPassword;
        set => SetProperty(ref _requiresPassword, value);
    }

    public bool IsPasswordIncorrect
    {
        get => _isPasswordIncorrect;
        set => SetProperty(ref _isPasswordIncorrect, value);
    }

    public bool HasOwnerPermissions
    {
        get => _hasOwnerPermissions;
        set => SetProperty(ref _hasOwnerPermissions, value);
    }

    public string AccessMessage
    {
        get => _accessMessage;
        set => SetProperty(ref _accessMessage, value);
    }

    public string GetEffectivePassword()
        => !string.IsNullOrWhiteSpace(OwnerPassword)
            ? OwnerPassword
            : Password;
}
