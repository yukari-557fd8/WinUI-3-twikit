using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace App2
{
    public sealed partial class NotificationsPage : Page
    {
        public NotificationsViewModel ViewModel { get; }

        public NotificationsPage()
        {
            ViewModel = App.ViewModels.Notifications;
            this.InitializeComponent();
            this.Loaded += NotificationsPage_Loaded;
        }

        private async void NotificationsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Notifications.Count == 0 && !ViewModel.IsLoading)
            {
                await ViewModel.LoadNotificationsAsync();
            }
            AttachScrollHandler();
        }

        private ScrollViewer? _scrollViewer;

        private void AttachScrollHandler()
        {
            if (NotificationsListView == null) return;

            _scrollViewer = ScrollPositionHelper.FindScrollViewer(NotificationsListView);
            if (_scrollViewer == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ ScrollViewer が見つかりませんでした");
                return;
            }

            _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            ScrollPositionHelper.RestoreOffset(_scrollViewer, ViewModel.ScrollVerticalOffset);
            System.Diagnostics.Debug.WriteLine("✅ ScrollViewer ハンドラ登録完了");
        }

        private async void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            if (!e.IsIntermediate)
            {
                ViewModel.ScrollVerticalOffset = scrollViewer.VerticalOffset;
            }

            if (ViewModel.IsLoading || ViewModel.IsLoadingMore || !ViewModel.HasMore)
            {
                return;
            }

            if (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight < scrollViewer.ExtentHeight - 150)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("📜 下までスクロール → 追加取得");
            App.MainWindow?.ShowLoading(true);
            try
            {
                await ViewModel.LoadMoreNotificationsAsync(refresh: false);
            }
            finally
            {
                if (!ViewModel.IsLoadingMore)
                {
                    App.MainWindow?.ShowLoading(false);
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ScrollPositionHelper.SaveOffset(_scrollViewer, offset => ViewModel.ScrollVerticalOffset = offset);
            base.OnNavigatedFrom(e);
        }
    }
}