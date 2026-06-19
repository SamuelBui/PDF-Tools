using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfTool.App.Services;

public class AppStatusService : IAppStatusService
{
    private string _statusText = "Ready";
    private bool _isBusy;
    private bool _isIndeterminate;
    private double _progressValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set => SetProperty(ref _isIndeterminate, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public void Start(string statusText, bool isIndeterminate = true, double progressValue = 0)
    {
        StatusText = statusText;
        ProgressValue = progressValue;
        IsIndeterminate = isIndeterminate;
        IsBusy = true;
    }

    public void Report(string statusText, double progressValue)
    {
        StatusText = statusText;
        ProgressValue = progressValue;
        IsIndeterminate = false;
    }

    public void Complete(string statusText)
    {
        StatusText = statusText;
        ProgressValue = 100;
        IsIndeterminate = false;
        IsBusy = false;
    }

    public void Fail(string statusText)
    {
        StatusText = statusText;
        ProgressValue = 0;
        IsIndeterminate = false;
        IsBusy = false;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
