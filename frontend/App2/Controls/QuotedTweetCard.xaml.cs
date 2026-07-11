using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;

namespace App2.Controls
{
    public sealed partial class QuotedTweetCard : UserControl
    {
        public static readonly DependencyProperty TweetProperty =
            DependencyProperty.Register(
                nameof(Tweet),
                typeof(TweetViewModel),
                typeof(QuotedTweetCard),
                new PropertyMetadata(null, OnTweetChanged));

        private static void OnTweetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is QuotedTweetCard card)
            {
                card.DataContext = e.NewValue;
                card.Visibility = e.NewValue is TweetViewModel tweet && tweet.HasQuotedTweet
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public TweetViewModel? Tweet
        {
            get => (TweetViewModel?)GetValue(TweetProperty);
            set => SetValue(TweetProperty, value);
        }

        private static readonly SolidColorBrush DefaultBorderBrush = new(Windows.UI.Color.FromArgb(255, 42, 42, 42));
        private static readonly SolidColorBrush HoverBorderBrush = new(Windows.UI.Color.FromArgb(255, 58, 58, 58));
        private static readonly SolidColorBrush DefaultBackgroundBrush = new(Windows.UI.Color.FromArgb(255, 26, 26, 26));
        private static readonly SolidColorBrush HoverBackgroundBrush = new(Windows.UI.Color.FromArgb(255, 37, 37, 37));

        public QuotedTweetCard()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        private void QuoteBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (Tweet?.QuotedTweet?.IsUnavailable != false)
            {
                return;
            }

            QuoteBorder.Background = HoverBackgroundBrush;
            QuoteBorder.BorderBrush = HoverBorderBrush;
        }

        private void QuoteBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            QuoteBorder.Background = DefaultBackgroundBrush;
            QuoteBorder.BorderBrush = DefaultBorderBrush;
        }

        private void QuoteBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 将来: 引用元ツイート詳細へ遷移
        }

        private async void MediaThumbnail_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not Image image)
            {
                return;
            }

            var mediaItem = (image.DataContext as MediaItem)
                ?? Tweet?.QuotedCardMediaItems.FirstOrDefault();
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

            if (mediaItem?.IsVideo == true)
            {
                await ShowFullScreenVideo(mediaUrl);
            }
            else
            {
                await ShowFullScreenImage(mediaUrl);
            }
        }

        private Size GetHostSize()
        {
            if (XamlRoot is { Size.Width: > 0, Size.Height: > 0 } root)
            {
                return root.Size;
            }

            return new Size(800, 600);
        }

        private ContentDialog CreateMediaDialog(UIElement content, Size hostSize)
        {
            var dialog = new ContentDialog
            {
                Title = null,
                Content = content,
                CloseButtonText = "閉じる",
                Background = new SolidColorBrush(Colors.Black),
                RequestedTheme = ElementTheme.Dark,
                FullSizeDesired = true,
                XamlRoot = XamlRoot
            };

            dialog.Resources["ContentDialogBackground"] = new SolidColorBrush(Colors.Black);
            dialog.Resources["ContentDialogMinWidth"] = hostSize.Width - 40;
            dialog.Resources["ContentDialogMinHeight"] = hostSize.Height - 40;

            return dialog;
        }

        private async Task ShowFullScreenImage(string imageUrl)
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                return;
            }

            var hostSize = GetHostSize();
            var bitmap = new BitmapImage(uri)
            {
                DecodePixelWidth = (int)(hostSize.Width * 0.9),
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = hostSize.Height * 0.9,
                MaxWidth = hostSize.Width * 0.9
            };

            var dialog = CreateMediaDialog(image, hostSize);
            await dialog.ShowAsync();
            image.Source = null;
        }

        private async Task ShowFullScreenVideo(string videoUrl)
        {
            if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
            {
                return;
            }

            var hostSize = GetHostSize();
            var fullScreenPlayer = new MediaPlayerElement
            {
                Source = MediaSource.CreateFromUri(uri),
                AutoPlay = true,
                AreTransportControlsEnabled = true,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxHeight = hostSize.Height * 0.9,
                MaxWidth = hostSize.Width * 0.9
            };

            var dialog = CreateMediaDialog(fullScreenPlayer, hostSize);
            await dialog.ShowAsync();

            fullScreenPlayer.MediaPlayer?.Pause();
            fullScreenPlayer.Source = null;
        }
    }
}