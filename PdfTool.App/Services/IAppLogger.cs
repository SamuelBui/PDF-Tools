namespace PdfTool.App.Services;

public interface IAppLogger
{
    string CurrentLogFilePath { get; }
    void ClearLogs();
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}
