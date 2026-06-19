using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PdfTool.App.Models;

namespace PdfTool.App.Behaviors;

public static class PageSelectionBehavior
{
    private static readonly DependencyProperty IsSweepSelectingProperty =
        DependencyProperty.RegisterAttached(
            "IsSweepSelectingInternal",
            typeof(bool),
            typeof(PageSelectionBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PageSelectionBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            listBox.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            listBox.PreviewMouseRightButtonUp += OnPreviewMouseRightButtonUp;
            listBox.PreviewMouseMove += OnPreviewMouseMove;
        }
        else
        {
            listBox.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
            listBox.PreviewMouseRightButtonUp -= OnPreviewMouseRightButtonUp;
            listBox.PreviewMouseMove -= OnPreviewMouseMove;
        }
    }

    private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        listBox.SetValue(IsSweepSelectingProperty, true);
        listBox.CaptureMouse();

        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
        {
            foreach (var page in listBox.Items.OfType<PdfPageOrganizerItem>())
            {
                page.IsSelected = false;
            }
        }

        SelectPageFromPointer(listBox, e.GetPosition(listBox));
        e.Handled = true;
    }

    private static void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if ((bool)listBox.GetValue(IsSweepSelectingProperty))
        {
            listBox.SetValue(IsSweepSelectingProperty, false);
            listBox.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if ((bool)listBox.GetValue(IsSweepSelectingProperty) && e.RightButton == MouseButtonState.Pressed)
        {
            SelectPageFromPointer(listBox, e.GetPosition(listBox));
            e.Handled = true;
        }
    }

    private static void SelectPageFromPointer(ListBox listBox, Point position)
    {
        var hit = listBox.InputHitTest(position) as DependencyObject;
        var page = VisualTreeBehaviorHelper.FindAncestor<ListBoxItem>(hit)?.DataContext as PdfPageOrganizerItem;
        if (page != null)
        {
            page.IsSelected = true;
        }
    }
}
