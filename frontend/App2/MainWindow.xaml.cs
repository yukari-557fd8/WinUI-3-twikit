using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using System.IO;
using Windows.UI;
using WinRT.Interop;

namespace App2
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer? _clockTimer;

        private void CustomizeWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // タイトルバーのアイコン
            var iconPath = Path.Combine(AppContext.BaseDirectory, "TwitterIcon48.ico");
            appWindow.SetIcon(iconPath);

            // Presenter を取得（標準の OverlappedPresenter）
            OverlappedPresenter? presenter = appWindow.Presenter as OverlappedPresenter;

            // #202020 を Color に変換
            var dark202020 = Color.FromArgb(255, 32, 32, 32); // #202020

            // タイトルバー背景
            appWindow.TitleBar.BackgroundColor = dark202020;
            appWindow.TitleBar.InactiveBackgroundColor = dark202020;

            // ボタン背景
            appWindow.TitleBar.ButtonBackgroundColor = dark202020;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = dark202020;

            // ボタン前景（アイコン）
            appWindow.TitleBar.ButtonForegroundColor = Colors.White;
            appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;

            // 必要なら最大化/最小化ボタンの制御
            if (presenter is not null)
            {
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }
        }
        public void SetWindowTitle(string pageName)
        {
            string appName = "WinUI 3 Twitter";  // ← アプリ名をここで統一管理
            string title = $"{pageName} / {appName}";

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Title = title;
        }


        public void ShowLoading(bool isLoading)
        {
            if (App.IsShuttingDown || LoadingPanel == null)
            {
                return;
            }

            LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        // ナビゲーション後にページがロードされたら初期状態をオフに
        private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            ShowLoading(false);
        }

        public MainWindow()
        {
            this.InitializeComponent();
            CustomizeWindow();
            InitializeTrayIcon();

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
            if (App.IsShuttingDown)
            {
                return;
            }

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

            if (App.IsShuttingDown)
            {
                return;
            }

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
        }

        private void ClockTimer_Tick(object? sender, object e)
        {
            if (App.IsShuttingDown || ClockText == null)
            {
                return;
            }

            ClockText.Text = DateTime.Now.ToString("HH:mm:ss") + "\n" + DateTime.Now.ToString("yyyy/MM/dd");
        }

        private void InitializeTrayIcon()
        {
            var openCommand = new XamlUICommand { Label = "開く" };
            openCommand.ExecuteRequested += TrayOpenCommand_ExecuteRequested;

            var exitCommand = new XamlUICommand { Label = "終了" };
            exitCommand.ExecuteRequested += TrayExitCommand_ExecuteRequested;

            TrayIcon.ContextFlyout = new MenuFlyout
            {
                Items =
                {
                    new MenuFlyoutItem { Command = openCommand },
                    new MenuFlyoutItem { Command = exitCommand },
                },
            };

            TrayIcon.ForceCreate();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (App.HandleClosedEvents && !App.IsShuttingDown)
            {
                args.Handled = true;
                this.Hide();
                return;
            }

            _clockTimer?.Stop();
            _clockTimer = null;

            App.ViewModels.Timeline.PropertyChanged -= Timeline_PropertyChanged;
            TrayIcon?.Dispose();
            App.NotifyMainWindowClosing();
            ServerManager.StopServer();
        }

        private void TrayOpenCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                this.Show();
                this.Activate();
            });
        }

        private void TrayExitCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                App.RequestShutdown();
                TrayIcon?.Dispose();
                this.Close();
            });
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                SetWindowTitle("設定");
                return;
            }

            var item = args.InvokedItemContainer as NavigationViewItem;
            switch (item?.Tag as string)
            {
                case "home":
                    ContentFrame.Navigate(typeof(TweetPage));
                    SetWindowTitle("ホーム");
                    break;

                case "timeline":
                    ContentFrame.Navigate(typeof(TimelinePage));
                    SetWindowTitle("タイムライン");
                    break;

                case "search":
                    ContentFrame.Navigate(typeof(SearchPage));
                    SetWindowTitle("検索");
                    break;

                case "notifications":
                    ContentFrame.Navigate(typeof(NotificationsPage));
                    SetWindowTitle("通知");
                    break;

                case "lists":
                    ContentFrame.Navigate(typeof(ListsPage));
                    SetWindowTitle("リスト");
                    break;

                case "myprofile":
                    ContentFrame.Navigate(typeof(MyProfilePage));
                    SetWindowTitle("プロフィール");
                    break;

                case "settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    SetWindowTitle("設定");
                    break;
            }
        }

    }
}
