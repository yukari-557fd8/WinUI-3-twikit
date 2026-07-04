using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace App2
{
    public sealed partial class TimelinePage : Page
    {
        public TimelineViewModel ViewModel { get; }

        public TimelinePage()
        {
            ViewModel = App.ViewModels.Timeline;
            this.InitializeComponent();
            this.Loaded += TimelinePage_Loaded;
        }

        private async void TimelinePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Tweets.Count == 0 && !ViewModel.IsLoading)
            {
                await ViewModel.LoadTweetsAsync();
            }
            AttachScrollHandler();
        }
        // ==================== 自動更新機能 ====================
        private DispatcherTimer? _autoUpdateTimer;

        private void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (AutoUpdateToggle.IsOn)
                StartAutoUpdate();
            else
                StopAutoUpdate();
        }

        private void StartAutoUpdate()
        {
            StopAutoUpdate();
            int interval = (int)PollingIntervalBox.Value;
            if (interval < 5) interval = 15;

            _autoUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(interval) };
            _autoUpdateTimer.Tick += async (s, args) => await PollNewTweetsAsync();
            _autoUpdateTimer.Start();
            System.Diagnostics.Debug.WriteLine($"🚀 自動更新開始 ({interval}秒間隔)");
        }

        private void StopAutoUpdate()
        {
            _autoUpdateTimer?.Stop();
            _autoUpdateTimer = null;
            System.Diagnostics.Debug.WriteLine("⛔ 自動更新停止");
        }

        private async Task PollNewTweetsAsync()
        {
            if (ViewModel.IsLoading || ViewModel.IsLoadingMore)
            {
                System.Diagnostics.Debug.WriteLine("⏭️ ポーリングスキップ (Loading中)");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 ポーリング開始 (type: {ViewModel.CurrentTimelineType})");

                var newTweets = await ViewModel.GetNewTweetsAsync();

                System.Diagnostics.Debug.WriteLine($"📥 GetNewTweetsAsync 結果: {newTweets.Count}件");

                if (newTweets.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ 新着なし");
                    return;
                }

                if (ViewModel.IsChronologicalTimeline)
                {
                    System.Diagnostics.Debug.WriteLine("📌 Latestモード → MergeAndSortNewTweets呼び出し");
                    ViewModel.MergeAndSortNewTweets(newTweets);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("📌 ForYouモード → 先頭挿入");
                    int added = 0;
                    foreach (var vm in newTweets)
                    {
                        if (!ViewModel.Tweets.Any(t => t.Id == vm.Id))
                        {
                            ViewModel.Tweets.Insert(0, vm);
                            added++;
                        }
                    }
                    if (added > 0)
                        System.Diagnostics.Debug.WriteLine($"✅ 先頭に {added}件追加");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ポーリング例外: {ex.Message}");
            }
        }        // SelectionChanged に変更
        private async void TimelineTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabView tabView && tabView.SelectedItem is TabViewItem tabItem &&
                tabItem.Tag is string type)
            {
                await ViewModel.SwitchTimelineAsync(type);
                AttachScrollHandler();
            }
        }

        private ScrollViewer? _scrollViewer;

        private void AttachScrollHandler()
        {
            if (TimelineListView == null) return;

            _scrollViewer = ScrollPositionHelper.FindScrollViewer(TimelineListView);
            if (_scrollViewer == null) return;

            _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            ScrollPositionHelper.RestoreOffset(_scrollViewer, ViewModel.ScrollVerticalOffset);
        }

        private async void MediaThumbnail_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is not Image image)
            {
                return;
            }

            var mediaItem = image.DataContext as MediaItem;
            var mediaUrl = image.Tag as string ?? mediaItem?.Url;

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                mediaUrl = image.Source switch
                {
                    BitmapImage bitmap => bitmap.UriSource?.ToString(),
                    _ => null
                };
            }

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return;
            }

            if (string.Equals(mediaItem?.Type, "video", StringComparison.OrdinalIgnoreCase))
            {
                await ShowFullScreenVideo(mediaUrl);
            }
            else
            {
                await ShowFullScreenImage(mediaUrl);
            }
        }

        private async Task ShowFullScreenVideo(string videoUrl)
        {
            if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
            {
                return;
            }

            var fullScreenPlayer = new MediaPlayerElement
            {
                Source = MediaSource.CreateFromUri(uri),
                AutoPlay = true,
                AreTransportControlsEnabled = true,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = this.ActualHeight * 0.9,
                MaxWidth = this.ActualWidth * 0.9
            };

            var dialog = new ContentDialog
            {
                Title = null,
                Content = fullScreenPlayer,
                CloseButtonText = "閉じる",
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                RequestedTheme = ElementTheme.Dark,
                FullSizeDesired = true,
                PrimaryButtonText = null
            };

            dialog.XamlRoot = this.XamlRoot;
            dialog.Resources["ContentDialogBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Black);
            dialog.Resources["ContentDialogMinWidth"] = this.ActualWidth - 40;
            dialog.Resources["ContentDialogMinHeight"] = this.ActualHeight - 40;

            await dialog.ShowAsync();

            fullScreenPlayer.MediaPlayer?.Pause();
            fullScreenPlayer.Source = null;
        }

        private async Task ShowFullScreenImage(string imageUrl)
        {
            var bitmap = new BitmapImage(new Uri(imageUrl))
            {
                DecodePixelWidth = 4096,
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };

            var imageControl = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,           // アスペクト比を保持
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = this.ActualHeight * 0.9, // ウィンドウの高さに近いサイズ
                MaxWidth = this.ActualWidth * 0.9
            };

            var dialog = new ContentDialog
            {
                Title = null,                        // タイトル非表示
                Content = imageControl,
                CloseButtonText = "閉じる",
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                RequestedTheme = ElementTheme.Dark,
                FullSizeDesired = true,               // できるだけ大きく
                PrimaryButtonText = null
            };

            dialog.XamlRoot = this.XamlRoot;

            // 背景を完全に黒く
            dialog.Resources["ContentDialogBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Black);

            // 余白を極力減らす
            dialog.Resources["ContentDialogMinWidth"] = this.ActualWidth - 40;
            dialog.Resources["ContentDialogMinHeight"] = this.ActualHeight - 40;

            await dialog.ShowAsync();
            imageControl.Source = null; // メモリ解放のためにソースをクリア
        }
        // ==================== ボタン処理 ====================

        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                try
                {
                    bool success = await ViewModel.LikeTweetAsync(vm.Id, vm.IsLiked);
                    if (success)
                    {
                        vm.ToggleLike();
                        System.Diagnostics.Debug.WriteLine($"いいね実行成功: {vm.Id}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"いいね失敗: {ex.Message}");
                }
            }
        }

        private async void RetweetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                try
                {
                    bool success = await ViewModel.RetweetTweetAsync(vm.Id);
                    if (success)
                    {
                        vm.ToggleRetweet();
                        System.Diagnostics.Debug.WriteLine($"リツイート実行成功: {vm.Id}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"リツイート失敗: {ex.Message}");
                }
            }
        }
        private void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                vm.IsReplying = !vm.IsReplying;   // トグル
                if (vm.IsReplying)
                    vm.ReplyText = "";
            }
        }

        private async void SendReply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                if (string.IsNullOrWhiteSpace(vm.ReplyText)) return;

                try
                {
                    var response = await ViewModel.ReplyTweetAsync(vm.Id, vm.ReplyText);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

                        if (result.TryGetProperty("new_tweet_id", out var newIdElement))
                        {
                            string newTweetId = newIdElement.GetString() ?? "";

                            // 返信をツイートの直下に挿入
                            ViewModel.AddReplyToTimeline(vm, newTweetId, vm.ReplyText);
                        }

                        // フォームを閉じる
                        vm.IsReplying = false;
                        vm.ReplyText = "";

                        System.Diagnostics.Debug.WriteLine($"リプライ送信＆表示成功: {vm.Id}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"リプライ失敗: {ex.Message}");
                }
            }
        }
        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                vm.IsReplying = false;
                vm.ReplyText = "";
            }
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

            if (ViewModel.IsLoading || ViewModel.IsLoadingMore)
            {
                return;
            }

            if (scrollViewer.VerticalOffset + scrollViewer.ViewportHeight < scrollViewer.ExtentHeight - 150)
            {
                return;
            }

            App.MainWindow?.ShowLoading(true);
            try
            {
                await ViewModel.LoadMoreTweetsAsync();
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
            StopAutoUpdate();
            base.OnNavigatedFrom(e);
        }
    }
}
