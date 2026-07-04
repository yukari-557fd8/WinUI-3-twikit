using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace App2
{
    public class TimelineViewModel : INotifyPropertyChanged
    {
        private const int DefaultTimelineFetchCount = 30;

        public ObservableCollection<TweetViewModel> Tweets { get; } = new();

        private readonly Dictionary<string, int> _tweetCountsByType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["for_you"] = 0,
            ["latest"] = 0
        };

        public int ForYouTweetCount => _tweetCountsByType.GetValueOrDefault("for_you");
        public int LatestTweetCount => _tweetCountsByType.GetValueOrDefault("latest");

        public TimelineViewModel()
        {
            Tweets.CollectionChanged += Tweets_CollectionChanged;
        }

        private void Tweets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "for_you" : CurrentTimelineType;
            _tweetCountsByType[key] = Tweets.Count;
            OnPropertyChanged(nameof(ForYouTweetCount));
            OnPropertyChanged(nameof(LatestTweetCount));
        }

        private static readonly HashSet<string> _seenIds = new HashSet<string>();

        private readonly HttpClient _httpClient = new HttpClient();

        private readonly Dictionary<string, HashSet<string>> _seenTweetIdsByType = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string?> _nextCursorByType = new(StringComparer.OrdinalIgnoreCase);

        private bool _isLoading = false;
        private bool _isLoadingMore = false;

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

        private string _currentTimelineType = "for_you";

        public string CurrentTimelineType
        {
            get => _currentTimelineType;
            set
            {
                if (_currentTimelineType != value)
                {
                    _currentTimelineType = value;
                    OnPropertyChanged(nameof(CurrentTimelineType));
                    OnPropertyChanged(nameof(IsChronologicalTimeline));
                }
            }
        }

        public bool IsChronologicalTimeline => !string.Equals(CurrentTimelineType, "for_you", StringComparison.OrdinalIgnoreCase);

        private readonly Dictionary<string, double> _scrollOffsetsByTimelineType = new(StringComparer.OrdinalIgnoreCase);

        public double ScrollVerticalOffset
        {
            get
            {
                var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "default" : CurrentTimelineType;
                return _scrollOffsetsByTimelineType.GetValueOrDefault(key);
            }
            set
            {
                var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "default" : CurrentTimelineType;
                _scrollOffsetsByTimelineType[key] = value;
            }
        }

        private HashSet<string> GetSeenSet()
        {
            var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "default" : CurrentTimelineType;
            if (!_seenTweetIdsByType.TryGetValue(key, out var seenSet))
            {
                seenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _seenTweetIdsByType[key] = seenSet;
            }

            return seenSet;
        }

        private string? GetNextCursor()
        {
            var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "default" : CurrentTimelineType;
            if (!_nextCursorByType.TryGetValue(key, out var cursor))
            {
                cursor = null;
                _nextCursorByType[key] = cursor;
            }

            return cursor;
        }

        private void SetNextCursor(string? cursor)
        {
            var key = string.IsNullOrWhiteSpace(CurrentTimelineType) ? "default" : CurrentTimelineType;
            _nextCursorByType[key] = cursor;
        }

        private TweetViewModel CreateTweetViewModel(TweetDto dto)
        {
            var vm = new TweetViewModel
            {
                Id = dto.id ?? "",
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

            return vm;
        }

        public async Task SwitchTimelineAsync(string newType)
        {
            if (CurrentTimelineType == newType) return;
            CurrentTimelineType = newType;
            await LoadTweetsAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public async Task LoadTweetsAsync()
        {
            IsLoading = true;
            Tweets.Clear();

            var seenSet = GetSeenSet();
            seenSet.Clear();
            SetNextCursor(null);

            await LoadMoreTweetsAsync();
            IsLoading = false;
        }

        public async Task LoadMoreTweetsAsync()
        {
            if (IsLoadingMore) return;

            IsLoadingMore = true;

            try
            {
                var (tweetList, nextCursor) = await FetchTimelinePageAsync(GetNextCursor());
                SetNextCursor(nextCursor);

                if (tweetList != null)
                {
                    var seenSet = GetSeenSet();

                    foreach (var dto in tweetList)
                    {
                        if (string.IsNullOrEmpty(dto.id) || dto.id == "error" || seenSet.Contains(dto.id)) continue;

                        seenSet.Add(dto.id);
                        Tweets.Add(CreateTweetViewModel(dto));
                    }

                    RebuildTimelineOrder();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("追加取得エラー: " + ex.Message);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void RebuildTimelineOrder()
        {
            if (!IsChronologicalTimeline)
            {
                return;
            }

            var sorted = Tweets
                .OrderByDescending(t => ParseTweetTime(t.CreatedAt))
                .ThenByDescending(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sorted.Count <= 1)
            {
                return;
            }

            for (int targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
            {
                var item = sorted[targetIndex];
                int currentIndex = Tweets.IndexOf(item);
                if (currentIndex != targetIndex)
                {
                    Tweets.Move(currentIndex, targetIndex);
                }
            }
        }

        private async Task<(List<TweetDto> TweetList, string? NextCursor)> FetchTimelinePageAsync(string? cursor)
        {
            var url = $"http://localhost:8000/timeline?pages=1&count={DefaultTimelineFetchCount}&type={Uri.EscapeDataString(CurrentTimelineType)}";
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            try
            {
                var payload = JsonSerializer.Deserialize<TimelineApiResponse>(json);
                return (payload?.tweets ?? new List<TweetDto>(), payload?.next_cursor);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timeline payload parse failed: {ex.Message}");
                var fallback = JsonSerializer.Deserialize<List<TweetDto>>(json);
                return (fallback ?? new List<TweetDto>(), null);
            }
        }

        private sealed class TimelineApiResponse
        {
            public List<TweetDto>? tweets { get; set; }
            public string? next_cursor { get; set; }
        }

        public void MergeAndSortNewTweets(List<TweetViewModel> newTweets)
        {
            if (newTweets == null || newTweets.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Merge: 新着0件");
                return;
            }

            Microsoft.UI.Dispatching.DispatcherQueue? dispatcher =
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                ?? App.MainWindow?.DispatcherQueue;


            if (dispatcher == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ DispatcherQueue が取得できませんでした");
                return;
            }

            dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
            {
                try
                {
                    var seenSet = GetSeenSet();
                    int added = 0;
                    var addedIds = new List<string>();

                    // 新着候補を新しい順に処理
                    foreach (var newVm in newTweets.OrderByDescending(t => ParseTweetTime(t.CreatedAt)))
                    {
                        if (string.IsNullOrEmpty(newVm.Id)) continue;
                        if (seenSet.Contains(newVm.Id) || Tweets.Any(t => t.Id == newVm.Id)) continue;

                        Tweets.Insert(0, newVm);
                        seenSet.Add(newVm.Id);
                        addedIds.Add(newVm.Id);
                        added++;
                    }

                    if (added > 0)
                    {
                        RebuildTimelineOrder();
                        System.Diagnostics.Debug.WriteLine($"✅ 本物新着追加: {added}件 (合計{Tweets.Count}件) | 例: {string.Join(", ", addedIds.Take(3))}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Merge: 本物追加0件（すべて重複） 取得数: {newTweets.Count}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Merge例外: {ex.Message}");
                }
            });
        }
        private DateTime ParseTweetTime(string createdAt)
        {
            if (string.IsNullOrWhiteSpace(createdAt))
                return DateTime.MinValue;

            if (DateTime.TryParse(createdAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var dt))
                return dt;

            if (DateTime.TryParseExact(createdAt, new[] { "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
                return dt;

            return DateTime.MinValue;
        }

        public async Task<bool> LikeTweetAsync(string tweetId, bool currentlyLiked)
        {
            try
            {
                string url = currentlyLiked ? $"http://localhost:8000/unlike/{tweetId}" : $"http://localhost:8000/like/{tweetId}";
                var response = await _httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Like API Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RetweetTweetAsync(string tweetId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"http://localhost:8000/retweet/{tweetId}", null);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Retweet API Error: {ex.Message}");
                return false;
            }
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
            int index = Tweets.IndexOf(originalVm);
            if (index >= 0)
            {
                Tweets.Insert(index + 1, replyVm);
            }
            else
            {
                Tweets.Add(replyVm);
            }

            System.Diagnostics.Debug.WriteLine($"返信を挿入しました: {newTweetId}");
        }
        public async Task<List<TweetViewModel>> GetNewTweetsAsync()
        {
            var newVms = new List<TweetViewModel>();
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔍 GetNewTweetsAsync 開始 (type: {CurrentTimelineType})");

                var (tweetList, _) = await FetchTimelinePageAsync(null);
                if (tweetList == null) return newVms;

                var seenSet = GetSeenSet();
                var seenInResponse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dto in tweetList)
                {
                    if (string.IsNullOrEmpty(dto.id) || dto.id == "error") continue;
                    if (seenSet.Contains(dto.id) || seenInResponse.Contains(dto.id)) continue;

                    seenInResponse.Add(dto.id);
                    newVms.Add(CreateTweetViewModel(dto));
                }

                System.Diagnostics.Debug.WriteLine($"📥 GetNewTweetsAsync 結果: {newVms.Count}件 (API取得後)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNewTweetsAsync Error: {ex.Message}");
            }

            return newVms;
        }
    }
}