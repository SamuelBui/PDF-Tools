using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PdfTool.App.Models;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Views;

public partial class MainWindow : Window
{
    private bool _sessionAutoRestored;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_sessionAutoRestored && DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.TryAutoRestoreSession(this);
            _sessionAutoRestored = true;
        }

        SyncProtectPasswordBoxes();
    }

    private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.AutoSaveSession(this);
        }
    }

    private void ProtectUserPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.UserPassword = passwordBox.Password;
        }
    }

    private void ProtectOwnerPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.OwnerPassword = passwordBox.Password;
        }
    }

    private void ProtectCommonPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel && sender is PasswordBox passwordBox)
        {
            mainViewModel.Protect.BatchCommonPassword = passwordBox.Password;
        }
    }

    private void ProtectToggleUserPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleUserPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectToggleOwnerPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleOwnerPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectToggleCommonPasswordVisibility_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ToggleCommonPasswordVisibility();
            SyncProtectPasswordBoxes();
        }
    }

    private void ProtectClearAllPasswords_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Protect.ClearAllPasswords();
            SyncProtectPasswordBoxes();
        }
    }

    private void MergeDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void MergeDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel ||
            !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files != null)
        {
            mainViewModel.Merge.AddFiles(files.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void MergeQueueList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.OpenSelectedFileCommand.Execute(null);
        }
    }

    private void MergeRotateLeftSelectedPages_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.RotateSelectedPages(-90);
        }
    }

    private void MergeRotateRightSelectedPages_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Merge.RotateSelectedPages(90);
        }
    }

    private void MergeRemoveSelectedFiles_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var selectedFiles = MergeQueueList.SelectedItems.Cast<PdfFileItem>().ToList();
        mainViewModel.Merge.RemoveFiles(selectedFiles);
    }

    private void SyncProtectPasswordBoxes()
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        ProtectUserPasswordBox.Password = mainViewModel.Protect.UserPassword;
        ProtectUserPasswordBoxCompact.Password = mainViewModel.Protect.UserPassword;
        ProtectOwnerPasswordBox.Password = mainViewModel.Protect.OwnerPassword;
        ProtectOwnerPasswordBoxCompact.Password = mainViewModel.Protect.OwnerPassword;
        ProtectBatchOwnerPasswordBoxCompact.Password = mainViewModel.Protect.OwnerPassword;
        ProtectCommonPasswordBox.Password = mainViewModel.Protect.BatchCommonPassword;
        ProtectCommonPasswordBoxCompact.Password = mainViewModel.Protect.BatchCommonPassword;
    }
}
