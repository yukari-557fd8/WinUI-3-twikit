using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media;           // MediaItem 用
using Microsoft.UI.Xaml.Media.Imaging;   // BitmapImage 用

using App2;   // TimelineViewModel 内の TweetViewModel にアクセス
namespace App2
{
    public class SearchViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TweetViewModel> SearchResults { get; } = new();

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly HashSet<string> _seenIds = new HashSet<string>();  // ← 重複防止用

        private bool _isLoading = false;
        private bool _isLoadingMore = false;
        private string? _lastCursor = null;
        private string? _currentQuery = null;
        private double _scrollVerticalOffset;

        public double ScrollVerticalOffset
        {
            get => _scrollVerticalOffset;
            set => _scrollVerticalOffset = value;
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set { if (_isLoadingMore != value) { _isLoadingMore = value; OnPropertyChanged(nameof(IsLoadingMore)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public async Task SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            IsLoading = true;
            SearchResults.Clear();
            _seenIds.Clear();                    // ← 追加
            _lastCursor = null;
            _currentQuery = query;

            try
            {
                await LoadMoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadMoreAsync()
        {
            if (IsLoadingMore || string.IsNullOrEmpty(_currentQuery)) return;

            IsLoadingMore = true;

            try
            {
                string url = $"http://localhost:8000/search?query={Uri.EscapeDataString(_currentQuery)}&count=20";
                if (!string.IsNullOrEmpty(_lastCursor))
                {
                    url += $"&cursor={Uri.EscapeDataString(_lastCursor)}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var tweetList = JsonSerializer.Deserialize<List<TweetDto>>(json);

                if (tweetList != null && tweetList.Count > 0)
                {
                    int added = 0;
                    foreach (var dto in tweetList)
                    {
                        if (string.IsNullOrEmpty(dto.id) || _seenIds.Contains(dto.id))
                            continue;

                        _seenIds.Add(dto.id);   // ← 重複防止

                        var vm = new TweetViewModel
                        {
                            Id = dto.id,
                            Text = dto.text ?? "",
                            UserName = dto.user_name ?? "",
                            UserScreenName = string.IsNullOrEmpty(dto.user_screen_name) ? "" : "@" + dto.user_screen_name,
                            CreatedAt = dto.created_at ?? "",
                            RetweetCount = dto.retweet_count,
                            FavoriteCount = dto.favorite_count,
                            ReplyCount = dto.reply_count,
                            MediaItems = dto.media_items?.Select(m =>
                            {
                                var thumbnail = m?.thumbnail ?? m?.url ?? "";
                                return new MediaItem
                                {
                                    Type = m?.type ?? "image",
                                    Url = m?.url ?? "",
                                    Thumbnail = thumbnail,
                                    ThumbnailImage = ImageCache.GetOrCreate(thumbnail, 640)
                                };
                            }).ToList() ?? new List<MediaItem>()
                        };

                        vm.UserProfileImage = ImageCache.GetOrCreate(dto.user_profile_image, 96);

                        SearchResults.Add(vm);
                        added++;
                    }
                    System.Diagnostics.Debug.WriteLine($"Search: {added}件追加 (重複除外済み)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Search LoadMore Error: " + ex.Message);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        // Like / Retweet（TimelineViewModelと同じ）
        public async Task<bool> LikeTweetAsync(string tweetId, bool currentlyLiked)
        {
            try
            {
                string url = currentlyLiked ? $"http://localhost:8000/unlike/{tweetId}" : $"http://localhost:8000/like/{tweetId}";
                var response = await _httpClient.PostAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> RetweetTweetAsync(string tweetId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"http://localhost:8000/retweet/{tweetId}", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<HttpResponseMessage> ReplyTweetAsync(string tweetId, string replyText)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { text = replyText }),
                    Encoding.UTF8,
                    "application/json");

                return await _httpClient.PostAsync($"http://localhost:8000/reply/{tweetId}", content);
            }
            catch
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
        }

        public void AddReplyToTimeline(TweetViewModel originalVm, string newTweetId, string replyText)
        {
            var replyVm = new TweetViewModel
            {
                Id = newTweetId,
                Text = replyText,
                UserName = "自分",
                UserScreenName = "",
                CreatedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                IsLiked = false,
                IsRetweeted = false,
                ReplyCount = 0,
                FavoriteCount = 0,
                RetweetCount = 0
            };


            // 元のツイートの直後に挿入
            int index = SearchResults.IndexOf(originalVm);
            if (index >= 0)
            {
                SearchResults.Insert(index + 1, replyVm);
            }
            else
            {
                SearchResults.Add(replyVm);
            }

            System.Diagnostics.Debug.WriteLine($"返信を挿入しました: {newTweetId}");
        }
    }
}