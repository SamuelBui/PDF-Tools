using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PdfTool.App.Models;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Behaviors;

public enum PdfDragDropMode
{
    None,
    MergeFileQueue,
    MergePageList,
    SplitPageList
}

public static class PdfDragDropBehavior
{
    private const string MergeFilesDragDataFormat = "PdfTool.App.MergeFiles";
    private const string MergePagesDragDataFormat = "PdfTool.App.MergePages";
    private const string SplitPagesDragDataFormat = "PdfTool.App.SplitPages";

    private static readonly DependencyProperty DragStartPointProperty =
        DependencyProperty.RegisterAttached(
            "DragStartPointInternal",
            typeof(Point?),
            typeof(PdfDragDropBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty ToggleCandidateProperty =
        DependencyProperty.RegisterAttached(
            "ToggleCandidateInternal",
            typeof(PdfPageOrganizerItem),
            typeof(PdfDragDropBehavior),
            new PropertyMetadata(null));

    private static readonly DependencyProperty DragTriggeredProperty =
        DependencyProperty.RegisterAttached(
            "DragTriggeredInternal",
            typeof(bool),
            typeof(PdfDragDropBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(PdfDragDropMode),
            typeof(PdfDragDropBehavior),
            new PropertyMetadata(PdfDragDropMode.None, OnModeChanged));

    public static PdfDragDropMode GetMode(DependencyObject obj)
        => (PdfDragDropMode)obj.GetValue(ModeProperty);

    public static void SetMode(DependencyObject obj, PdfDragDropMode value)
        => obj.SetValue(ModeProperty, value);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        Detach(listBox);

        if ((PdfDragDropMode)e.NewValue == PdfDragDropMode.None)
        {
            listBox.AllowDrop = false;
            return;
        }

        listBox.AllowDrop = true;
        listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        listBox.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        listBox.PreviewMouseMove += OnPreviewMouseMove;
        listBox.DragOver += OnDragOver;
        listBox.DragLeave += OnDragLeave;
        listBox.Drop += OnDrop;
    }

    private static void Detach(ListBox listBox)
    {
        listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        listBox.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        listBox.PreviewMouseMove -= OnPreviewMouseMove;
        listBox.DragOver -= OnDragOver;
        listBox.DragLeave -= OnDragLeave;
        listBox.Drop -= OnDrop;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        listBox.SetValue(DragTriggeredProperty, false);
        listBox.SetValue(ToggleCandidateProperty, null);

        if (GetMode(listBox) == PdfDragDropMode.MergeFileQueue && IsFromNestedListBox(listBox, e.OriginalSource as DependencyObject))
        {
            listBox.SetValue(DragStartPointProperty, null);
            return;
        }

        listBox.SetValue(DragStartPointProperty, e.GetPosition(listBox));

        if (GetMode(listBox) == PdfDragDropMode.MergePageList)
        {
            var clickedPage = VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfPageOrganizerItem;
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None && clickedPage?.IsSelected == true)
            {
                listBox.SetValue(ToggleCandidateProperty, clickedPage);
                e.Handled = true;
            }
        }
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var toggleCandidate = listBox.GetValue(ToggleCandidateProperty) as PdfPageOrganizerItem;
        if (GetMode(listBox) == PdfDragDropMode.MergePageList &&
            toggleCandidate != null &&
            !(bool)listBox.GetValue(DragTriggeredProperty))
        {
            toggleCandidate.IsSelected = false;
            e.Handled = true;
        }

        ResetDragState(listBox);
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var dragStart = (Point?)listBox.GetValue(DragStartPointProperty);
        if (dragStart == null ||
            !VisualTreeBehaviorHelper.HasExceededDragThreshold(e.GetPosition(listBox), dragStart.Value))
        {
            return;
        }

        switch (GetMode(listBox))
        {
            case PdfDragDropMode.MergeFileQueue:
                StartMergeFileDrag(listBox, e);
                break;
            case PdfDragDropMode.MergePageList:
                StartMergePageDrag(listBox, e);
                break;
            case PdfDragDropMode.SplitPageList:
                StartSplitPageDrag(listBox, e);
                break;
        }
    }

    private static void StartMergeFileDrag(ListBox listBox, MouseEventArgs e)
    {
        if (IsFromNestedListBox(listBox, e.OriginalSource as DependencyObject))
        {
            return;
        }

        var selectedFiles = listBox.Items.OfType<PdfFileItem>()
            .Where(file => listBox.SelectedItems.Contains(file))
            .ToList();
        if (selectedFiles.Count == 0)
        {
            return;
        }

        var sourceItem = VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem?.DataContext is not PdfFileItem sourceFile || !selectedFiles.Contains(sourceFile))
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(MergeFilesDragDataFormat, selectedFiles);
        listBox.SetValue(DragTriggeredProperty, true);
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
        ResetDragState(listBox);
    }

    private static void StartMergePageDrag(ListBox listBox, MouseEventArgs e)
    {
        var selectedPages = listBox.Items.OfType<PdfPageOrganizerItem>()
            .Where(page => listBox.SelectedItems.Contains(page))
            .ToList();
        if (selectedPages.Count == 0)
        {
            return;
        }

        var sourceItem = VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem?.DataContext is not PdfPageOrganizerItem sourcePage || !selectedPages.Contains(sourcePage))
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(MergePagesDragDataFormat, selectedPages);
        listBox.SetValue(DragTriggeredProperty, true);
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
        ResetDragState(listBox);
    }

    private static void StartSplitPageDrag(ListBox listBox, MouseEventArgs e)
    {
        var selectedPages = listBox.Items.OfType<PdfPageOrganizerItem>()
            .Where(page => listBox.SelectedItems.Contains(page))
            .ToList();
        if (selectedPages.Count == 0)
        {
            return;
        }

        var sourceItem = VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (sourceItem?.DataContext is not PdfPageOrganizerItem sourcePage || !selectedPages.Contains(sourcePage))
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(SplitPagesDragDataFormat, selectedPages);
        listBox.SetValue(DragTriggeredProperty, true);
        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
        ResetDragState(listBox);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var mainViewModel = FindMainViewModel(listBox);
        switch (GetMode(listBox))
        {
            case PdfDragDropMode.MergeFileQueue:
                VisualTreeBehaviorHelper.AutoScroll(listBox, e.GetPosition(listBox), Orientation.Vertical);
                mainViewModel?.Merge.SetFileDropTarget(GetTargetFile(e));
                e.Effects = e.Data.GetDataPresent(MergeFilesDragDataFormat)
                    ? DragDropEffects.Move
                    : e.Data.GetDataPresent(DataFormats.FileDrop)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
                break;
            case PdfDragDropMode.MergePageList:
                VisualTreeBehaviorHelper.AutoScroll(listBox, e.GetPosition(listBox), Orientation.Horizontal);
                mainViewModel?.Merge.SetPageDropTarget(listBox.DataContext as PdfFileItem, GetTargetPage(e));
                e.Effects = e.Data.GetDataPresent(MergePagesDragDataFormat) || e.Data.GetDataPresent(MergeFilesDragDataFormat)
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
                break;
            case PdfDragDropMode.SplitPageList:
                VisualTreeBehaviorHelper.AutoScroll(listBox, e.GetPosition(listBox), Orientation.Vertical);
                mainViewModel?.Split.SetDropTarget(GetTargetPage(e));
                e.Effects = e.Data.GetDataPresent(SplitPagesDragDataFormat)
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
                break;
            default:
                e.Effects = DragDropEffects.None;
                break;
        }

        e.Handled = true;
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            ClearDropTargets(listBox);
            e.Handled = true;
        }
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var mainViewModel = FindMainViewModel(listBox);
        if (mainViewModel == null)
        {
            ClearDropTargets(listBox);
            return;
        }

        ClearDropTargets(listBox);

        switch (GetMode(listBox))
        {
            case PdfDragDropMode.MergeFileQueue:
                DropOnMergeFileQueue(e, mainViewModel);
                break;
            case PdfDragDropMode.MergePageList:
                DropOnMergePageList(listBox, e, mainViewModel);
                break;
            case PdfDragDropMode.SplitPageList:
                DropOnSplitPageList(e, mainViewModel);
                break;
        }

        e.Handled = true;
    }

    private static void DropOnMergeFileQueue(DragEventArgs e, MainViewModel mainViewModel)
    {
        if (e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            var draggedFiles = e.Data.GetData(MergeFilesDragDataFormat) as List<PdfFileItem>;
            if (draggedFiles is { Count: > 0 })
            {
                mainViewModel.Merge.ReorderFiles(draggedFiles, GetTargetFile(e));
            }

            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                mainViewModel.Merge.AddFiles(files.Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)));
            }
        }
    }

    private static void DropOnMergePageList(ListBox listBox, DragEventArgs e, MainViewModel mainViewModel)
    {
        var parentFile = listBox.DataContext as PdfFileItem;
        if (parentFile == null)
        {
            return;
        }

        var targetPage = GetTargetPage(e);
        if (e.Data.GetDataPresent(MergePagesDragDataFormat))
        {
            var draggedPages = e.Data.GetData(MergePagesDragDataFormat) as List<PdfPageOrganizerItem>;
            if (draggedPages is { Count: > 0 })
            {
                mainViewModel.Merge.InsertPagesIntoFile(parentFile, draggedPages, targetPage);
            }

            return;
        }

        if (e.Data.GetDataPresent(MergeFilesDragDataFormat))
        {
            var draggedFiles = e.Data.GetData(MergeFilesDragDataFormat) as List<PdfFileItem>;
            if (draggedFiles is { Count: > 0 })
            {
                mainViewModel.Merge.InsertFilesIntoFile(parentFile, draggedFiles, targetPage);
            }
        }
    }

    private static void DropOnSplitPageList(DragEventArgs e, MainViewModel mainViewModel)
    {
        if (!e.Data.GetDataPresent(SplitPagesDragDataFormat))
        {
            return;
        }

        var draggedPages = e.Data.GetData(SplitPagesDragDataFormat) as List<PdfPageOrganizerItem>;
        if (draggedPages is not { Count: > 0 })
        {
            return;
        }

        mainViewModel.Split.ReorderPages(draggedPages, GetTargetPage(e));
    }

    private static PdfFileItem? GetTargetFile(DragEventArgs e)
        => VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfFileItem;

    private static PdfPageOrganizerItem? GetTargetPage(DragEventArgs e)
        => VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PdfPageOrganizerItem;

    private static bool IsFromNestedListBox(ListBox owner, DependencyObject? source)
        => VisualTreeBehaviorHelper.FindAncestor<ListBox>(source) is ListBox innerList &&
           !ReferenceEquals(innerList, owner);

    private static void ResetDragState(ListBox listBox)
    {
        listBox.SetValue(DragStartPointProperty, null);
        listBox.SetValue(ToggleCandidateProperty, null);
        listBox.SetValue(DragTriggeredProperty, false);
    }

    private static void ClearDropTargets(ListBox listBox)
    {
        var mainViewModel = FindMainViewModel(listBox);
        if (mainViewModel == null)
        {
            return;
        }

        switch (GetMode(listBox))
        {
            case PdfDragDropMode.MergeFileQueue:
            case PdfDragDropMode.MergePageList:
                mainViewModel.Merge.ClearDropTargets();
                break;
            case PdfDragDropMode.SplitPageList:
                mainViewModel.Split.ClearDropTargets();
                break;
        }
    }

    private static MainViewModel? FindMainViewModel(DependencyObject dependencyObject)
    {
        var current = dependencyObject;
        while (current != null)
        {
            if (current is FrameworkElement element && element.DataContext is MainViewModel mainViewModel)
            {
                return mainViewModel;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return Application.Current.Windows
            .OfType<Window>()
            .Select(window => window.DataContext)
            .OfType<MainViewModel>()
            .FirstOrDefault();
    }
}
