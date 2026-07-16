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

namespace App2
{
    public sealed partial class ListsPage : Page
    {
        public ListsViewModel ViewModel { get; }

        public ListsPage()
        {
            ViewModel = App.ViewModels.Lists;
            InitializeComponent();
            Loaded += ListsPage_Loaded;
        }

        private async void ListsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Lists.Count == 0 && !ViewModel.IsLoading)
            {
                await ViewModel.LoadListsAsync();
            }

            AttachScrollHandler();
        }

        private void BreadcrumbList_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.BackToListCatalog();
            AttachScrollHandler();
        }

        private async void ListsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ListItemViewModel listItem)
            {
                return;
            }

            await ViewModel.SelectListAsync(listItem.Id, listItem.Name);
            AttachScrollHandler();
        }

        private ScrollViewer? _scrollViewer;

        private void AttachScrollHandler()
        {
            var targetListView = ViewModel.ShowTimeline ? TimelineListView : ListsListView;
            if (targetListView == null)
            {
                return;
            }

            _scrollViewer = ScrollPositionHelper.FindScrollViewer(targetListView);
            if (_scrollViewer == null)
            {
                return;
            }

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

            if (!ViewModel.ShowTimeline || ViewModel.IsLoading || ViewModel.IsLoadingMore)
            {
                return;
            }

            if (sv.VerticalOffset + sv.ViewportHeight < sv.ExtentHeight - 150)
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
            base.OnNavigatedFrom(e);
        }

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
            {
                return;
            }

            var vm = ReplyInputHelper.FindTweetViewModel(element);
            if (vm == null || vm.IsReplySending)
            {
                return;
            }

            ReplyInputHelper.SyncReplyTextFromInput(element, vm);
            if (string.IsNullOrWhiteSpace(vm.ReplyText))
            {
                return;
            }

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

        private void CancelReply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TweetViewModel vm)
            {
                if (vm.IsReplySending) return;

                vm.IsReplying = false;
                vm.ReplyText = string.Empty;
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
                DecodePixelWidth = (int)(this.ActualWidth * 0.9),
                CreateOptions = BitmapCreateOptions.IgnoreImageCache,
            };

            var imageControl = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = ActualHeight * 0.9,
                MaxWidth = ActualWidth * 0.9,
            };

            var dialog = new ContentDialog
            {
                Content = imageControl,
                CloseButtonText = "閉じる",
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                RequestedTheme = ElementTheme.Dark,
                FullSizeDesired = true,
                XamlRoot = XamlRoot,
                Resources =
                {
                    ["ContentDialogBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    ["ContentDialogMinWidth"] = ActualWidth - 40,
                    ["ContentDialogMinHeight"] = ActualHeight - 40,
                }
            };

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
                MaxHeight = ActualHeight * 0.9,
                MaxWidth = ActualWidth * 0.9,
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
                XamlRoot = XamlRoot,
                Resources =
                {
                    ["ContentDialogBackground"] = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    ["ContentDialogMinWidth"] = ActualWidth - 40,
                    ["ContentDialogMinHeight"] = ActualHeight - 40,
                }
            };

            await dialog.ShowAsync();

            fullScreenPlayer.MediaPlayer?.Pause();
            fullScreenPlayer.Source = null;
        }
    }
}
