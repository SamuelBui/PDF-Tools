using System.IO;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Models;

public class ProtectBatchItem : BaseViewModel
{
    private string _inputPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _password = string.Empty;
    private string _ownerPassword = string.Empty;
    private string _status = "Pending";
    private bool _isPasswordVisible;
    private bool _isOwnerPasswordVisible;
    private bool _hasValidationError;
    private bool _hasValidationWarning;

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

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(MaskedPassword));
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
                OnPropertyChanged(nameof(MaskedOwnerPassword));
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                HasValidationError = value.StartsWith("Error:", StringComparison.Ordinal);
                HasValidationWarning = value.StartsWith("Warning:", StringComparison.Ordinal);
            }
        }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public bool IsOwnerPasswordVisible
    {
        get => _isOwnerPasswordVisible;
        set => SetProperty(ref _isOwnerPasswordVisible, value);
    }

    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
    }

    public bool HasValidationWarning
    {
        get => _hasValidationWarning;
        private set => SetProperty(ref _hasValidationWarning, value);
    }

    public string MaskedPassword => string.IsNullOrEmpty(Password) ? string.Empty : new string('\u2022', Password.Length);
    public string MaskedOwnerPassword => string.IsNullOrEmpty(OwnerPassword) ? string.Empty : new string('\u2022', OwnerPassword.Length);
}
