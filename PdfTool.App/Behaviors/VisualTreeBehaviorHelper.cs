using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfTool.App.Behaviors;

public static class VisualTreeBehaviorHelper
{
    public static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    public static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nestedMatch = FindDescendant<T>(child);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    public static bool HasExceededDragThreshold(Point currentPosition, Point dragStart)
        => Math.Abs(currentPosition.X - dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance ||
           Math.Abs(currentPosition.Y - dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance;

    public static void AutoScroll(ItemsControl itemsControl, Point pointerPosition, Orientation orientation)
    {
        const double edgeThreshold = 56;
        const double scrollStep = 8;

        var scrollViewer = FindDescendant<ScrollViewer>(itemsControl);
        if (scrollViewer == null)
        {
            return;
        }

        if (orientation == Orientation.Vertical)
        {
            if (pointerPosition.Y <= edgeThreshold)
            {
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollViewer.VerticalOffset - scrollStep));
            }
            else if (pointerPosition.Y >= itemsControl.ActualHeight - edgeThreshold)
            {
                scrollViewer.ScrollToVerticalOffset(Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + scrollStep));
            }

            return;
        }

        if (pointerPosition.X <= edgeThreshold)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollViewer.HorizontalOffset - scrollStep));
        }
        else if (pointerPosition.X >= itemsControl.ActualWidth - edgeThreshold)
        {
            scrollViewer.ScrollToHorizontalOffset(Math.Min(scrollViewer.ScrollableWidth, scrollViewer.HorizontalOffset + scrollStep));
        }
    }
}
