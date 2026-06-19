using System.Collections.ObjectModel;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IRecentFilesService
{
    ReadOnlyObservableCollection<RecentFileItem> Files { get; }
    void AddFile(string filePath);
    void ClearAll();
    void ClearMissingFiles();
}
