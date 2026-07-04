using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace App2
{
    public sealed partial class SearchPage : Page
    {
        public SearchViewModel ViewModel { get; }

        public SearchPage()
        {
            ViewModel = App.ViewModels.Search;
            this.InitializeComponent();
            this.Loaded += SearchPage_Loaded;
        }

        private void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(FocusState.Programmatic);
            AttachScrollHandler();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                await PerformSearch();
        }

        private async Task PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) return;
            await ViewModel.SearchAsync(SearchBox.Text.Trim());
        }

        private ScrollViewer? _scrollViewer;

        private void AttachScrollHandler()
        {
            if (SearchListView == null) return;

            _scrollViewer = ScrollPositionHelper.FindScrollViewer(SearchListView);
            if (_scrollViewer == null) return;

            _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            ScrollPositionHelper.RestoreOffset(_scrollViewer, ViewModel.ScrollVerticalOffset);
        }

        private async void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is not ScrollViewer sv)
            {
                return;
            }

            if (!e.IsIntermediate)
            {
                ViewModel.ScrollVerticalOffset = sv.VerticalOffset;
            }

            if (!ViewModel.IsLoadingMore &&
                sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - 200)
            {
                App.MainWindow?.ShowLoading(true);
                await ViewModel.LoadMoreAsync();
                App.MainWindow?.ShowLoading(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ScrollPositionHelper.SaveOffset(_scrollViewer, offset => ViewModel.ScrollVerticalOffset = offset);
            base.OnNavigatedFrom(e);
        }

        // Like / Retweet ボタン
        private async void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                bool success = await ViewModel.LikeTweetAsync(vm.Id, vm.IsLiked);
                if (success) vm.ToggleLike();
            }
        }

        private async void RetweetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                bool success = await ViewModel.RetweetTweetAsync(vm.Id);
                if (success) vm.ToggleRetweet();
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
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = this.ActualHeight * 0.9,
                MaxWidth = this.ActualWidth * 0.9
            };

            var dialog = new ContentDialog
            {
                Title = null,
                Content = imageControl,
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
            imageControl.Source = null;
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
    }
}