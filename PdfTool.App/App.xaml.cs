using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PdfTool.App.Helpers;
using PdfTool.App.Services;
using PdfTool.App.ViewModels;
using PdfTool.App.Views;

namespace PdfTool.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IAppLogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        PdfiumFileHelper.ClearTemporaryCopies();

        var services = new ServiceCollection();

        services.AddSingleton<IAppLogger, AppLogger>();
        services.AddSingleton<IAppStatusService, AppStatusService>();
        services.AddSingleton<IAppSessionService, AppSessionService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IPdfDocumentInspectorService, PdfDocumentInspectorService>();
        services.AddSingleton<IPdfCompressionInspectorService, PdfCompressionInspectorService>();
        services.AddSingleton<IPdfImageObjectOptimizationService, PdfImageObjectOptimizationService>();
        services.AddSingleton<IPdfThumbnailService, PdfThumbnailService>();
        services.AddSingleton<IPdfProtectionService, PdfProtectionService>();
        services.AddSingleton<IPdfSplitService, PdfSplitService>();
        services.AddSingleton<IPdfMergeService, PdfMergeService>();
        services.AddSingleton<IPdfCompressionService, PdfCompressionService>();

        services.AddTransient<ProtectViewModel>();
        services.AddTransient<SplitViewModel>();
        services.AddTransient<MergeViewModel>();
        services.AddTransient<CompressViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<IAppLogger>();
        RegisterGlobalExceptionHandlers();
        _logger.LogInfo("Application startup.");

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        window.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        PdfiumFileHelper.ClearTemporaryCopies();
        _logger?.LogInfo($"Application shutdown with code {e.ApplicationExitCode}.");
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.LogError("Unhandled UI exception.", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred. The incident was logged to:{Environment.NewLine}{_logger?.CurrentLogFilePath}",
                "PDF Utility Tool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            _logger?.LogError("Unhandled AppDomain exception.", args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
    }
}
