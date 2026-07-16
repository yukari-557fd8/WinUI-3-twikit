using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace App2
{
    internal static class ScrollPositionHelper
    {
        public static ScrollViewer? FindScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(depObj, i));
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public static void SaveOffset(ScrollViewer? scrollViewer, Action<double> setOffset)
        {
            if (scrollViewer != null)
            {
                setOffset(scrollViewer.VerticalOffset);
            }
        }

        public static void RestoreOffset(ScrollViewer? scrollViewer, double offset)
        {
            if (App.IsShuttingDown || scrollViewer == null || offset <= 0)
            {
                return;
            }

            scrollViewer.DispatcherQueue.TryEnqueue(() =>
                RestoreOffsetDeferred(scrollViewer, offset));
        }

        private static void RestoreOffsetDeferred(ScrollViewer scrollViewer, double offset, int attempt = 0)
        {
            if (App.IsShuttingDown)
            {
                return;
            }

            try
            {
                if (scrollViewer.ExtentHeight >= offset + scrollViewer.ViewportHeight || attempt >= 15)
                {
                    scrollViewer.ChangeView(null, offset, null, true);
                    return;
                }

                scrollViewer.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Low,
                    () => RestoreOffsetDeferred(scrollViewer, offset, attempt + 1));
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80000013))
            {
                // UI がすでに破棄されている場合は復元を中止する
            }
        }
    }
}
