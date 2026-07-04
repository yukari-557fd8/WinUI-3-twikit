using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;

namespace App2
{
    public sealed partial class MainWindow : Window
    {
        public void ShowLoading(bool isLoading)
        {
            if (LoadingPanel != null)
            {
                LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ナビゲーション後にページがロードされたら初期状態をオフに
        private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            ShowLoading(false);
        }

        public MainWindow()
        {
            this.InitializeComponent();

            // NavView初期化...
            NavView.SelectedItem = NavView.MenuItems[0];
            this.ContentFrame.Navigate(typeof(TweetPage));

            this.Closed += MainWindow_Closed;
            this.ContentFrame.Navigated += ContentFrame_Navigated;  // ← 追加

            App.ViewModels.Timeline.PropertyChanged += Timeline_PropertyChanged;
            UpdateTweetCountDisplay();

            _ = InitializeServerAndClock();
        }

        private void Timeline_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TimelineViewModel.ForYouTweetCount)
                or nameof(TimelineViewModel.LatestTweetCount))
            {
                UpdateTweetCountDisplay();
            }
        }

        private void UpdateTweetCountDisplay()
        {
            var timeline = App.ViewModels.Timeline;
            if (ForYouTweetCountText != null)
            {
                ForYouTweetCountText.Text = $"おすすめ: {timeline.ForYouTweetCount}";
            }
            if (LatestTweetCountText != null)
            {
                LatestTweetCountText.Text = $"最新: {timeline.LatestTweetCount}";
            }
        }

        private async System.Threading.Tasks.Task InitializeServerAndClock()
        {
            await ServerManager.StartServerAsync(ServerStatusText);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) =>
            {
                ClockText.Text = DateTime.Now.ToString("HH:mm:ss") + "\n" + DateTime.Now.ToString("yyyy/MM/dd");
            };
            timer.Start();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            ServerManager.StopServer();
        }

        private void NavView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            var item = args.InvokedItemContainer as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            switch (item?.Tag as string)
            {
                case "home": ContentFrame.Navigate(typeof(TweetPage)); break;
                case "timeline": ContentFrame.Navigate(typeof(TimelinePage)); break;
                case "search": ContentFrame.Navigate(typeof(SearchPage)); break;
                case "notifications": ContentFrame.Navigate(typeof(NotificationsPage)); break;
                case "myprofile": ContentFrame.Navigate(typeof(MyProfilePage)); break;
                case "settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
            }
        }
    }
}