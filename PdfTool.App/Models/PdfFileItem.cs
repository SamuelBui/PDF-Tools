using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Models;

public class PdfFileItem : BaseViewModel
{
    private string _filePath = string.Empty;
    private string _password = string.Empty;
    private ImageSource? _thumbnail;
    private int? _pageCount;
    private bool _isEncrypted;
    private bool _requiresPassword;
    private bool _isPasswordIncorrect;
    private bool _isLocked;
    private bool _isDuplicate;
    private bool _isValidPdf = true;
    private string _validationMessage = "Pending validation.";
    private bool _isDropTarget;

    public PdfFileItem()
    {
        PageThumbnails = new ObservableCollection<PdfPageOrganizerItem>();
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    public ObservableCollection<PdfPageOrganizerItem> PageThumbnails { get; }

    public int? PageCount
    {
        get => _pageCount;
        set
        {
            if (SetProperty(ref _pageCount, value))
            {
                OnPropertyChanged(nameof(PageCountText));
            }
        }
    }

    public string PageCountText => PageCount.HasValue ? $"{PageCount.Value} page(s)" : "Page count unavailable";

    public bool IsEncrypted
    {
        get => _isEncrypted;
        set
        {
            if (SetProperty(ref _isEncrypted, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public bool RequiresPassword
    {
        get => _requiresPassword;
        set
        {
            if (SetProperty(ref _requiresPassword, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public bool IsPasswordIncorrect
    {
        get => _isPasswordIncorrect;
        set
        {
            if (SetProperty(ref _isPasswordIncorrect, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public bool IsDuplicate
    {
        get => _isDuplicate;
        set
        {
            if (SetProperty(ref _isDuplicate, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public bool IsValidPdf
    {
        get => _isValidPdf;
        set
        {
            if (SetProperty(ref _isValidPdf, value))
            {
                NotifyValidationStateChanged();
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    public bool NeedsAttention => IsDuplicate || IsLocked || RequiresPassword || IsPasswordIncorrect || !IsValidPdf;

    private void NotifyValidationStateChanged()
    {
        OnPropertyChanged(nameof(NeedsAttention));
    }
}
