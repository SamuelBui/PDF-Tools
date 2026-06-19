using System.Windows;
using PdfTool.App.Commands;
using PdfTool.App.Helpers;
using PdfTool.App.Models;
using PdfTool.App.Services;

namespace PdfTool.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IAppSessionService _sessionService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IAppLogger _logger;
    private int _selectedTabIndex;

    public MainViewModel(
        ProtectViewModel protect,
        SplitViewModel split,
        MergeViewModel merge,
        CompressViewModel compress,
        IAppStatusService status,
        IAppSessionService sessionService,
        IRecentFilesService recentFilesService,
        IAppLogger logger)
    {
        Protect = protect;
        Split = split;
        Merge = merge;
        Compress = compress;
        Status = status;
        _sessionService = sessionService;
        _recentFilesService = recentFilesService;
        _logger = logger;
        ClearLocalDataCommand = new RelayCommand(ClearLocalData);
    }

    public ProtectViewModel Protect { get; }
    public SplitViewModel Split { get; }
    public MergeViewModel Merge { get; }
    public CompressViewModel Compress { get; }
    public IAppStatusService Status { get; }
    public RelayCommand ClearLocalDataCommand { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public void TryAutoRestoreSession(Window window)
    {
        var session = _sessionService.TryLoadAutoSession();
        if (session == null)
        {
            return;
        }

        RestoreSessionState(window, session);
        try
        {
            // Immediately rewrite the auto-session without secrets so older session files are scrubbed on first run.
            _sessionService.SaveAutoSession(CaptureSessionState(window));
        }
        catch
        {
            // Session scrubbing should never block startup.
        }
        Status.Complete("Last session restored.");
    }

    public void AutoSaveSession(Window window)
    {
        try
        {
            _sessionService.SaveAutoSession(CaptureSessionState(window));
        }
        catch
        {
            // Auto-save should never block shutdown.
        }
    }

    private void ClearLocalData()
    {
        Protect.ClearTransientData();
        Split.ClearTransientData();
        Merge.ClearTransientData();
        Compress.ClearTransientData();
        _recentFilesService.ClearAll();
        _sessionService.ClearAutoSession();
        PdfiumFileHelper.ClearTemporaryCopies();
        _logger.ClearLogs();
        Status.Complete("Local thumbnails, recent file links, session, logs, and cache were cleared.");
    }

    private AppSessionData CaptureSessionState(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;

        return new AppSessionData
        {
            SelectedTabIndex = SelectedTabIndex,
            Window = new WindowSessionState
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = window.WindowState
            },
            Protect = Protect.CaptureSessionState(),
            Split = Split.CaptureSessionState(),
            Merge = Merge.CaptureSessionState(),
            Compress = Compress.CaptureSessionState()
        };
    }

    private void RestoreSessionState(Window window, AppSessionData session)
    {
        ApplyWindowState(window, session.Window);
        Protect.RestoreSessionState(session.Protect);
        Split.RestoreSessionState(session.Split);
        Merge.RestoreSessionState(session.Merge);
        Compress.RestoreSessionState(session.Compress);
        SelectedTabIndex = Math.Clamp(session.SelectedTabIndex, 0, 4);
    }

    private static void ApplyWindowState(Window window, WindowSessionState state)
    {
        var primaryWorkArea = SystemParameters.WorkArea;
        var targetWidth = state.Width > 0 ? Math.Min(state.Width, primaryWorkArea.Width) : Math.Min(window.Width, primaryWorkArea.Width);
        var targetHeight = state.Height > 0 ? Math.Min(state.Height, primaryWorkArea.Height) : Math.Min(window.Height, primaryWorkArea.Height);

        if (targetWidth > 0)
        {
            window.Width = targetWidth;
        }

        if (targetHeight > 0)
        {
            window.Height = targetHeight;
        }

        window.WindowState = WindowState.Normal;
        window.Left = primaryWorkArea.Left + Math.Max(0, (primaryWorkArea.Width - window.Width) / 2);
        window.Top = primaryWorkArea.Top + Math.Max(0, (primaryWorkArea.Height - window.Height) / 2);

        var restoredState = state.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : state.WindowState;

        window.WindowState = restoredState;
    }
}
