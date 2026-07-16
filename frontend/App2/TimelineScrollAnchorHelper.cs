using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.Foundation;

namespace App2
{
    internal static class TimelineScrollAnchorHelper
    {
        public static bool TryCaptureAnchor(ListView listView, ScrollViewer scrollViewer, out TimelineScrollAnchor anchor)
        {
            anchor = TimelineScrollAnchor.Empty;

            if (App.IsShuttingDown || listView == null || scrollViewer == null || listView.Items.Count == 0)
            {
                return false;
            }

            TweetViewModel? anchorTweet = null;
            double offsetWithinItem = 0;
            double bestTop = double.MaxValue;

            foreach (var item in listView.Items)
            {
                if (item is not TweetViewModel tweet)
                {
                    continue;
                }

                if (listView.ContainerFromItem(item) is not ListViewItem container || container.ActualHeight <= 0)
                {
                    continue;
                }

                var topLeft = container.TransformToVisual(scrollViewer).TransformPoint(new Point(0, 0));
                double itemTop = topLeft.Y;
                double itemBottom = itemTop + container.ActualHeight;

                if (itemBottom <= 0 || itemTop >= scrollViewer.ViewportHeight)
                {
                    continue;
                }

                if (itemTop < bestTop)
                {
                    bestTop = itemTop;
                    anchorTweet = tweet;
                    offsetWithinItem = Math.Max(0, -itemTop);
                }
            }

            if (anchorTweet != null)
            {
                anchor = new TimelineScrollAnchor
                {
                    TweetDedupKey = anchorTweet.DedupKey,
                    OffsetWithinItem = offsetWithinItem
                };
                return true;
            }

            if (scrollViewer.VerticalOffset < 1 && listView.Items[0] is TweetViewModel firstTweet)
            {
                anchor = new TimelineScrollAnchor
                {
                    TweetDedupKey = firstTweet.DedupKey,
                    OffsetWithinItem = 0
                };
                return true;
            }

            return false;
        }

        public static void SaveAnchor(ListView? listView, ScrollViewer? scrollViewer, Action<TimelineScrollAnchor> setAnchor)
        {
            if (listView == null || scrollViewer == null)
            {
                return;
            }

            if (TryCaptureAnchor(listView, scrollViewer, out var anchor))
            {
                setAnchor(anchor);
            }
        }

        public static void RestoreAnchor(
            ListView listView,
            ScrollViewer scrollViewer,
            TimelineScrollAnchor anchor,
            Action? onComplete = null)
        {
            if (App.IsShuttingDown || anchor.IsEmpty || listView == null || scrollViewer == null)
            {
                onComplete?.Invoke();
                return;
            }

            scrollViewer.DispatcherQueue.TryEnqueue(() =>
                RestoreAnchorDeferred(listView, scrollViewer, anchor, onComplete));
        }

        private static void RestoreAnchorDeferred(
            ListView listView,
            ScrollViewer scrollViewer,
            TimelineScrollAnchor anchor,
            Action? onComplete,
            int attempt = 0)
        {
            if (App.IsShuttingDown)
            {
                onComplete?.Invoke();
                return;
            }

            try
            {
                var tweet = listView.Items
                    .OfType<TweetViewModel>()
                    .FirstOrDefault(t => string.Equals(t.DedupKey, anchor.TweetDedupKey, StringComparison.OrdinalIgnoreCase));

                if (tweet == null)
                {
                    if (attempt < 15)
                    {
                        scrollViewer.DispatcherQueue.TryEnqueue(
                            DispatcherQueuePriority.Low,
                            () => RestoreAnchorDeferred(listView, scrollViewer, anchor, onComplete, attempt + 1));
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }

                    return;
                }

                listView.ScrollIntoView(tweet, ScrollIntoViewAlignment.Leading);

                if (listView.ContainerFromItem(tweet) is not ListViewItem container || container.ActualHeight <= 0)
                {
                    if (attempt < 20)
                    {
                        scrollViewer.DispatcherQueue.TryEnqueue(
                            DispatcherQueuePriority.Low,
                            () => RestoreAnchorDeferred(listView, scrollViewer, anchor, onComplete, attempt + 1));
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }

                    return;
                }

                if (anchor.OffsetWithinItem > 0)
                {
                    var targetOffset = scrollViewer.VerticalOffset + anchor.OffsetWithinItem;
                    scrollViewer.ChangeView(null, targetOffset, null, true);
                }

                onComplete?.Invoke();
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80000013))
            {
                onComplete?.Invoke();
            }
        }
    }
}
