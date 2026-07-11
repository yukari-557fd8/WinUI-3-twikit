using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace App2
{
    public partial class NotificationsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<NotificationViewModel> Notifications { get; } = [];

        private readonly HttpClient _httpClient = new();
        private bool _isLoading = false;
        private bool _isLoadingMore = false;
        private bool _hasMore = true;
        private double _scrollVerticalOffset;

        public double ScrollVerticalOffset
        {
            get => _scrollVerticalOffset;
            set => _scrollVerticalOffset = value;
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set { _isLoadingMore = value; OnPropertyChanged(nameof(IsLoadingMore)); }
        }

        public bool HasMore
        {
            get => _hasMore;
            private set { _hasMore = value; OnPropertyChanged(nameof(HasMore)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public async Task LoadNotificationsAsync()
        {
            IsLoading = true;
            Notifications.Clear();
            HasMore = true;
            await LoadMoreNotificationsAsync(refresh: true);
            IsLoading = false;
        }

        /// <summary>
        /// 通知を取得
        /// </summary>
        /// <param name="refresh">true: 最新から取得 / false: 続きを取得</param>
        public async Task LoadMoreNotificationsAsync(bool refresh = false)
        {
            if (IsLoadingMore || (!refresh && !HasMore)) return;

            IsLoadingMore = true;

            try
            {
                var url = $"http://localhost:8000/notifications?count=20&refresh={refresh.ToString().ToLower()}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var newNotifs = JsonSerializer.Deserialize<List<NotificationDto>>(json);

                if (newNotifs == null || newNotifs.Count == 0)
                {
                    if (!refresh)
                    {
                        HasMore = false;
                    }
                    return;
                }

                int added = 0;
                foreach (var dto in newNotifs)
                {
                    if (Notifications.Any(n => n.Id == dto.id)) continue;

                    var vm = new NotificationViewModel
                    {
                        Id = dto.id ?? "",
                        Type = dto.type ?? "",
                        Text = dto.text ?? "",
                        ActorName = dto.actor_name ?? "",
                        ActorScreenName = dto.actor_screen_name ?? "",
                        CreatedAt = dto.created_at ?? "",
                        TargetTweetText = dto.target_tweet_text ?? ""
                    };

                    if (!string.IsNullOrEmpty(dto.actor_profile_image))
                    {
                        try
                        {
                            vm.ActorProfileImage = new BitmapImage(new Uri(dto.actor_profile_image));
                        }
                        catch { }
                    }

                    Notifications.Add(vm);
                    added++;
                }
                System.Diagnostics.Debug.WriteLine($"追加通知: {added}件");

                if (!refresh && added == 0)
                {
                    HasMore = false;
                }
                else if (refresh)
                {
                    SortNotificationsByTime();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadMore Error: " + ex.Message);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void SortNotificationsByTime()
        {
            var sorted = Notifications
                .OrderByDescending(vm => TimeDisplayHelper.TryParse(vm.CreatedAt) ?? DateTime.MinValue)
                .ThenByDescending(vm => vm.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sorted.Count <= 1)
            {
                return;
            }

            for (int targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
            {
                var item = sorted[targetIndex];
                int currentIndex = Notifications.IndexOf(item);
                if (currentIndex != targetIndex)
                {
                    Notifications.Move(currentIndex, targetIndex);
                }
            }
        }

    }

    // DTO と ViewModel（変更なし）
    public class NotificationDto
    {
        public string? id { get; set; }
        public string? type { get; set; }
        public string? text { get; set; }
        public string? actor_name { get; set; }
        public string? actor_screen_name { get; set; }
        public string? actor_profile_image { get; set; }
        public string? created_at { get; set; }
        public string? target_tweet_text { get; set; }
    }

    public partial class NotificationViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string ActorScreenName { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CreatedAtDisplay => TimeDisplayHelper.FormatDisplay(CreatedAt);
        public string TargetTweetText { get; set; } = string.Empty;
        public ImageSource? ActorProfileImage { get; set; }

        public string TypeText => Type?.ToLower() switch
        {
            "favorite" or "like" => "いいねしました",
            "retweet" or "repost" => "リポストしました",
            "reply" => "返信しました",
            "follow" => "フォローしました",
            _ => Type ?? "通知"
        };

        public bool HasTargetTweet => !string.IsNullOrEmpty(TargetTweetText);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
