using CommunityToolkit.WinUI.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace App2
{
    // ここにMediaFileを移動
    public class MediaFile
    {
        public string FilePath { get; set; } = string.Empty;

        private BitmapImage? _preview;
        public BitmapImage? Preview
        {
            get => _preview;
            set
            {
                _preview = value;
                // 通知を追加（バインディング安定化）
            }
        }
    }
    public sealed partial class TweetPage : Page
    {
        public ObservableCollection<MediaFile> SelectedFiles { get; } = [];

        // MediaFile クラスを少し強化


        public class ResultData
        {
            public string? result { get; set; }
        }

        // メディア削除処理
        private void RemoveMedia_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MediaFile media)
            {
                SelectedFiles.Remove(media);
                UpdateSelectedMediaInfo();
            }
        }

        public TweetPage()
        {
            this.InitializeComponent();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int len = InputBox.Text?.Length ?? 0;
            CountText.Text = $"{len} 文字";
        }

        private void InputBox_CtrlEnterInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (SendButton?.IsEnabled == true)
            {
                OnSendClick(SendButton, new RoutedEventArgs());
            }
            args.Handled = true;
        }

        // メディア選択情報を更新（件数 + 合計サイズ）
        private void UpdateSelectedMediaInfo()
        {
            if (SelectedFiles.Count > 0)
            {
                long totalBytes = 0;
                foreach (var media in SelectedFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(media.FilePath);
                        totalBytes += fileInfo.Length;
                    }
                    catch { }
                }

                string countText = $"{SelectedFiles.Count} 件のメディア";
                string sizeText = FormatFileSize(totalBytes);

                SelectedMediaCount.Text = $"{countText} ({sizeText})";
            }
            else
            {
                SelectedMediaCount.Text = "";
            }
        }

        // バイト数を読みやすい形式に変換
        private static string FormatFileSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                < KB => $"{bytes} B",
                < MB => $"{(double)bytes / KB:F1} KB",
                < GB => $"{(double)bytes / MB:F1} MB",
                _ => $"{(double)bytes / GB:F2} GB"
            };
        }

        private async void OnAddMediaClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                FileTypeFilter =
                {
                    ".pjp",
                    ".jfif",
                    ".jpe",
                    ".pjpeg",
                    ".jpeg",
                    ".jpg",
                    ".png",
                    ".webp",
                    ".gif",
                    ".m4v",
                    ".mp4",
                    ".mov"
                }
            };

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();

            foreach (var file in files.Take(4))
            {
                var media = new MediaFile { FilePath = file.Path };
                try
                {
                    var uri = new Uri("file:///" + file.Path.Replace("\\", "/"));
                    media.Preview = new BitmapImage(uri);
                    System.Diagnostics.Debug.WriteLine($"Preview loaded: {file.Name}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview failed: {ex.Message}");
                    // 失敗時も空のまま追加
                }
                SelectedFiles.Add(media);
            }

            UpdateSelectedMediaInfo();
        }

        // 画像ファイル判定ヘルパー
        private static bool IsImageFile(string fileType)
        {
            var imageTypes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".jfif" };
            return imageTypes.Contains(fileType.ToLower());
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text.Replace("\r", "\r\n");
            if (string.IsNullOrWhiteSpace(text) && SelectedFiles.Count == 0)
                return;

            App.MainWindow?.ShowLoading(true);   // ← 追加（PaneFooterにも表示）
            StartPosting();

            await SendTweetWithMediaAsync(text);

            EndPosting();
            App.MainWindow?.ShowLoading(false);  // ← 追加

            // 完全クリア
            InputBox.Text = string.Empty;
            SelectedFiles.Clear();
            SelectedMediaCount.Text = "";
            CountText.Text = "0 文字";
        }


        private async Task SendTweetWithMediaAsync(string text)
        {
            using var httpClient = new HttpClient();
            using var multipart = new MultipartFormDataContent();

            multipart.Add(new StringContent(text), "text");

            foreach (var media in SelectedFiles)
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(media.FilePath);
                var stream = await storageFile.OpenReadAsync();
                var streamContent = new StreamContent(stream.AsStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multipart.Add(streamContent, "files", storageFile.Name);
            }

            try
            {
                var response = await httpClient.PostAsync("http://localhost:8000/tweet", multipart);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ResultData>(resultJson);

                var message = result?.result ?? "ツイート完了";
                OutputTextBlock.Text = message;

                try
                {
                    new ToastContentBuilder().AddText(message).Show();
                }
                catch { }
            }
            catch (Exception ex)
            {
                OutputTextBlock.Text = $"ツイート失敗: {ex.Message}";
            }
        }

        private void StartPosting()
        {
            if (SendButton != null)
            {
                SendButton.IsEnabled = false;
                SendButton.Content = "ツイート中...";
            }

            if (PostingProgressRing != null)
            {
                PostingProgressRing.IsActive = true;
                PostingProgressRing.Visibility = Visibility.Visible;
            }
        }

        private void EndPosting()
        {
            if (SendButton != null)
            {
                SendButton.IsEnabled = true;
                SendButton.Content = "ツイートする";
            }

            if (PostingProgressRing != null)
            {
                PostingProgressRing.IsActive = false;
                PostingProgressRing.Visibility = Visibility.Collapsed;
            }
        }
    }

}
