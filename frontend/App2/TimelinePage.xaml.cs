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
            this.Unloaded += (_, _) => StopAutoUpdate();
        }

        private async void TimelinePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Tweets.Count == 0 && !ViewModel.IsLoading)
            {
                await ViewModel.LoadTweetsAsync();
            }
            AttachScrollHandler(restoreAnchor: !ViewModel.ScrollAnchor.IsEmpty);
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
            System.Diagnostics.Debug.WriteLine($"自動更新開始 ({interval}秒間隔)");
        }

        private void StopAutoUpdate()
        {
            _autoUpdateTimer?.Stop();
            _autoUpdateTimer = null;
            System.Diagnostics.Debug.WriteLine("自動更新停止");
        }

        private async Task PollNewTweetsAsync()
        {
            if (ViewModel.IsLoading || ViewModel.IsLoadingMore)
            {
                System.Diagnostics.Debug.WriteLine("ポーリングスキップ (Loading中)");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"ポーリング開始 (type: {ViewModel.CurrentTimelineType})");

                var newTweets = await ViewModel.GetNewTweetsAsync();

                System.Diagnostics.Debug.WriteLine($"GetNewTweetsAsync 結果: {newTweets.Count}件");

                if (newTweets.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("新着なし");
                    return;
                }

                if (ViewModel.IsChronologicalTimeline)
                {
                    System.Diagnostics.Debug.WriteLine("Latestモード → MergeAndSortNewTweets呼び出し");
                    ViewModel.MergeAndSortNewTweets(newTweets);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ForYouモード → 先頭挿入");
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
                        System.Diagnostics.Debug.WriteLine($"先頭に {added}件追加");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ポーリング例外: {ex.Message}");
            }
        }        // SelectionChanged に変更
        private async void TimelineTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabView tabView && tabView.SelectedItem is TabViewItem tabItem &&
                tabItem.Tag is string type)
            {
                TimelineScrollAnchorHelper.SaveAnchor(TimelineListView, _scrollViewer, anchor => ViewModel.ScrollAnchor = anchor);
                await ViewModel.SwitchTimelineAsync(type);
                AttachScrollHandler(restoreAnchor: true);
            }
        }

        private ScrollViewer? _scrollViewer;
        private bool _isRestoringScroll;

        private void AttachScrollHandler(bool restoreAnchor = false)
        {
            if (TimelineListView == null) return;

            _scrollViewer = ScrollPositionHelper.FindScrollViewer(TimelineListView);
            if (_scrollViewer == null) return;

            _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;

            if (restoreAnchor && !ViewModel.ScrollAnchor.IsEmpty)
            {
                _isRestoringScroll = true;
                TimelineScrollAnchorHelper.RestoreAnchor(
                    TimelineListView,
                    _scrollViewer,
                    ViewModel.ScrollAnchor,
                    onComplete: () => _isRestoringScroll = false);
            }
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
        private void ReplyForm_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!ReplyInputHelper.IsCtrlEnter(e) || sender is not DependencyObject depObj)
            {
                return;
            }

            if (ReplyInputHelper.TrySendReply(depObj, s => SendReply_Click(s, new RoutedEventArgs())))
            {
                e.Handled = true;
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
                PrimaryButtonText = null,
                XamlRoot = this.XamlRoot
            };
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
                DecodePixelWidth = (int)(this.ActualWidth * 0.9),
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
                PrimaryButtonText = null,
                XamlRoot = this.XamlRoot
            };

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
                await TweetActionHandler.HandleLikeAsync(
                    vm,
                    ViewModel.LikeTweetAsync,
                    ViewModel.FindTweetById);
            }
        }

        private async void RetweetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: TweetViewModel vm })
            {
                await TweetActionHandler.HandleRetweetAsync(
                    vm,
                    ViewModel.RetweetTweetAsync,
                    ViewModel.FindTweetById);
            }
        }

        private void QuoteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: TweetViewModel vm })
            {
                vm.BeginQuoting();
            }
        }

        private void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                vm.IsReplying = !vm.IsReplying;
                if (vm.IsReplying)
                {
                    vm.ReplyText = string.Empty;
                    vm.CancelQuoting();
                }
            }
        }

        private async void SendReply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            var vm = ReplyInputHelper.FindTweetViewModel(element);
            if (vm == null || vm.IsReplySending) return;

            ReplyInputHelper.SyncReplyTextFromInput(element, vm);
            if (string.IsNullOrWhiteSpace(vm.ReplyText)) return;

            var replyText = vm.ReplyText;
            var replySucceeded = false;
            vm.IsReplySending = true;
            try
            {
                var response = await ViewModel.ReplyTweetAsync(vm.Id, replyText);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();

                    if (result.TryGetProperty("new_tweet_id", out var newIdElement))
                    {
                        var newTweetId = newIdElement.GetString() ?? string.Empty;
                        ViewModel.AddReplyToTimeline(vm, newTweetId, replyText);
                    }

                    replySucceeded = true;
                    System.Diagnostics.Debug.WriteLine($"リプライ送信＆表示成功: {vm.Id}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"リプライ失敗: {ex.Message}");
            }
            finally
            {
                vm.IsReplySending = false;
                if (replySucceeded)
                {
                    vm.IsReplying = false;
                    vm.ReplyText = string.Empty;
                }
            }
        }
        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                if (vm.IsReplySending) return;

                vm.IsReplying = false;
                vm.ReplyText = "";
            }
        }

        private void QuoteForm_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!ReplyInputHelper.IsCtrlEnter(e) || sender is not DependencyObject depObj)
            {
                return;
            }

            if (ReplyInputHelper.TrySendQuote(depObj, s => SendQuote_Click(s, new RoutedEventArgs())))
            {
                e.Handled = true;
            }
        }

        private async void SendQuote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var vm = ReplyInputHelper.FindTweetViewModel(element);
            if (vm == null || vm.IsQuoteSending || string.IsNullOrEmpty(vm.Id))
            {
                return;
            }

            if (!TweetActionRequestGuard.TryBeginQuote(vm.Id))
            {
                return;
            }

            ReplyInputHelper.SyncQuoteTextFromInput(element, vm);
            if (string.IsNullOrWhiteSpace(vm.QuoteText))
            {
                TweetActionRequestGuard.EndQuote(vm.Id);
                return;
            }

            var quoteText = vm.QuoteText;
            var quoteSucceeded = false;
            vm.IsQuoteSending = true;
            try
            {
                var response = await ViewModel.QuoteTweetAsync(vm.Id, quoteText);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("new_tweet_id", out var newIdElement))
                    {
                        var newTweetId = newIdElement.GetString() ?? string.Empty;
                        ViewModel.AddQuoteToTimeline(vm, newTweetId, quoteText);
                    }

                    quoteSucceeded = true;
                    System.Diagnostics.Debug.WriteLine($"引用ツイート送信＆表示成功: {vm.Id}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引用ツイート失敗: {ex.Message}");
            }
            finally
            {
                vm.IsQuoteSending = false;
                TweetActionRequestGuard.EndQuote(vm.Id);
                if (quoteSucceeded)
                {
                    vm.CancelQuoting();
                }
            }
        }

        private void CancelQuote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                vm.CancelQuoting();
            }
        }

        private async void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            if (!e.IsIntermediate && !_isRestoringScroll)
            {
                TimelineScrollAnchorHelper.SaveAnchor(TimelineListView, scrollViewer, anchor => ViewModel.ScrollAnchor = anchor);
            }

            if (ViewModel.IsLoading || ViewModel.IsLoadingMore || _isRestoringScroll)
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
            TimelineScrollAnchorHelper.SaveAnchor(TimelineListView, _scrollViewer, anchor => ViewModel.ScrollAnchor = anchor);
            StopAutoUpdate();
            base.OnNavigatedFrom(e);
        }
    }
}
