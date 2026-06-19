using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class RecentFilesService : IRecentFilesService
{
    private const int MaxItems = 10;
    private readonly ObservableCollection<RecentFileItem> _files = new();
    private readonly string _storagePath;

    public RecentFilesService()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfTool.App");

        Directory.CreateDirectory(appFolder);
        _storagePath = Path.Combine(appFolder, "recent-files.json");
        Files = new ReadOnlyObservableCollection<RecentFileItem>(_files);

        Load();
        ClearMissingFiles();
    }

    public ReadOnlyObservableCollection<RecentFileItem> Files { get; }

    public void AddFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        var existing = _files.FirstOrDefault(x => string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _files.Remove(existing);
        }

        _files.Insert(0, new RecentFileItem
        {
            FilePath = filePath,
            LastUsedUtc = DateTime.UtcNow
        });

        while (_files.Count > MaxItems)
        {
            _files.RemoveAt(_files.Count - 1);
        }

        Save();
    }

    public void ClearMissingFiles()
    {
        var missingFiles = _files.Where(x => !File.Exists(x.FilePath)).ToList();
        foreach (var missingFile in missingFiles)
        {
            _files.Remove(missingFile);
        }

        Save();
    }

    public void ClearAll()
    {
        _files.Clear();

        try
        {
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
        }
        catch
        {
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_storagePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var items = JsonSerializer.Deserialize<List<RecentFileItem>>(json) ?? new List<RecentFileItem>();

            foreach (var item in items
                         .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                         .OrderByDescending(x => x.LastUsedUtc)
                         .Take(MaxItems))
            {
                _files.Add(item);
            }
        }
        catch
        {
            _files.Clear();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_files.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_storagePath, json);
    }
}
