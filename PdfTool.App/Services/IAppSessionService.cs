using PdfTool.App.Models;

namespace PdfTool.App.Services;

public interface IAppSessionService
{
    void SaveAutoSession(AppSessionData session);
    AppSessionData? TryLoadAutoSession();
    void ClearAutoSession();
}
