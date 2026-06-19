using System.ComponentModel;

namespace PdfTool.App.Services;

public interface IAppStatusService : INotifyPropertyChanged
{
    string StatusText { get; }
    bool IsBusy { get; }
    bool IsIndeterminate { get; }
    double ProgressValue { get; }
    void Start(string statusText, bool isIndeterminate = true, double progressValue = 0);
    void Report(string statusText, double progressValue);
    void Complete(string statusText);
    void Fail(string statusText);
}
