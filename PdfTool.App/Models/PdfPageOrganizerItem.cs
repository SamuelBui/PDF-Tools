using System.IO;
using PdfTool.App.ViewModels;
using System.Windows.Media;

namespace PdfTool.App.Models;

public class PdfPageOrganizerItem : BaseViewModel
{
    private bool _isSelected;
    private bool _isDropTarget;
    private int _pageNumber;
    private int _rotation;
    private ImageSource? _thumbnail;

    public int PageNumber
    {
        get => _pageNumber;
        set
        {
            if (SetProperty(ref _pageNumber, value))
            {
                OnPropertyChanged(nameof(PageLabel));
            }
        }
    }

    public int SourcePageNumber { get; init; }
    public string SourceFilePath { get; init; } = string.Empty;
    public string SourcePassword { get; set; } = string.Empty;
    public double WidthPoints { get; init; }
    public double HeightPoints { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }

    public int Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, NormalizeRotation(value)))
            {
                OnPropertyChanged(nameof(RotationLabel));
            }
        }
    }

    public string PageLabel => $"{PageNumber:00}";

    public string RotationLabel => Rotation == 0 ? "0 deg" : $"{Rotation} deg";

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = rotation % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
