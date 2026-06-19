using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PdfTool.App.Models;

namespace PdfTool.App.Services;

public class AppSessionService : IAppSessionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PdfTool.App.Session.v1");
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _autoSessionPath;

    public AppSessionService()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfTool.App");

        Directory.CreateDirectory(appFolder);
        _autoSessionPath = Path.Combine(appFolder, "last-session.pdftoolsession");
    }

    public void SaveAutoSession(AppSessionData session)
    {
        var path = _autoSessionPath;
        var json = JsonSerializer.SerializeToUtf8Bytes(session, _serializerOptions);
        var protectedBytes = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, protectedBytes);
    }

    public AppSessionData? TryLoadAutoSession()
    {
        if (!File.Exists(_autoSessionPath))
        {
            return null;
        }

        var path = _autoSessionPath;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var json = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AppSessionData>(json, _serializerOptions) ?? new AppSessionData();
        }
        catch
        {
            return null;
        }
    }

    public void ClearAutoSession()
    {
        try
        {
            if (File.Exists(_autoSessionPath))
            {
                File.Delete(_autoSessionPath);
            }
        }
        catch
        {
            // Session cleanup should not block the app.
        }
    }
}
