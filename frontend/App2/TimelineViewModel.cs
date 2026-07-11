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

namespace App2
{
    public partial class TimelineViewModel : INotifyPropertyChanged
    {
        private const int DefaultTimelineFetchCount = 30;

        public ObservableCollection<TweetViewModel> Tweets { get; } = [];

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

        private static readonly HashSet<string> _seenIds = [];

        private readonly HttpClient _httpClient = new();

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

        private static TweetViewModel CreateTweetViewModel(TweetDto dto) => TweetViewModel.FromDto(dto);

        private static string GetDedupKey(TweetDto dto)
            => !string.IsNullOrWhiteSpace(dto.timeline_entry_id) ? dto.timeline_entry_id : dto.id ?? string.Empty;

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
                        var dedupKey = GetDedupKey(dto);
                        if (string.IsNullOrEmpty(dedupKey) || dedupKey == "error" || seenSet.Contains(dedupKey)) continue;

                        seenSet.Add(dedupKey);
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
                .OrderByDescending(t => t.SortKey)
                .ThenByDescending(t => TimeDisplayHelper.TryParse(t.CreatedAt) ?? DateTime.MinValue)
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
                return (payload?.tweets ?? [], payload?.next_cursor);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timeline payload parse failed: {ex.Message}");
                var fallback = JsonSerializer.Deserialize<List<TweetDto>>(json);
                return (fallback ?? [], null);
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
                System.Diagnostics.Debug.WriteLine("Merge: 新着0件");
                return;
            }

            Microsoft.UI.Dispatching.DispatcherQueue? dispatcher =
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                ?? App.MainWindow?.DispatcherQueue;


            if (dispatcher == null)
            {
                System.Diagnostics.Debug.WriteLine("DispatcherQueue が取得できませんでした");
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
                    foreach (var newVm in newTweets.OrderByDescending(t => t.SortKey))
                    {
                        if (string.IsNullOrEmpty(newVm.DedupKey)) continue;
                        if (seenSet.Contains(newVm.DedupKey) || Tweets.Any(t => t.DedupKey == newVm.DedupKey)) continue;

                        Tweets.Insert(0, newVm);
                        seenSet.Add(newVm.DedupKey);
                        addedIds.Add(newVm.DedupKey);
                        added++;
                    }

                    if (added > 0)
                    {
                        RebuildTimelineOrder();
                        System.Diagnostics.Debug.WriteLine($"本物新着追加: {added}件 (合計{Tweets.Count}件) | 例: {string.Join(", ", addedIds.Take(3))}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Merge: 本物追加0件（すべて重複） 取得数: {newTweets.Count}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Merge例外: {ex.Message}");
                }
            });
        }
        public TweetViewModel? FindTweetById(string tweetId)
            => Tweets.FirstOrDefault(t => string.Equals(t.Id, tweetId, StringComparison.Ordinal));

        public Task<bool> LikeTweetAsync(string tweetId, bool currentlyLiked)
            => TweetActionClient.LikeAsync(_httpClient, tweetId, currentlyLiked);

        public Task<bool> RetweetTweetAsync(string tweetId)
            => TweetActionClient.RetweetAsync(_httpClient, tweetId);

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
                TimelineEntryId = newTweetId,
                Text = replyText,
                UserName = "八雲ゆかり",
                UserScreenName = "@yukari_557fd8",
                CreatedAt = TimeDisplayHelper.FormatNowForStorage(),
                IsLiked = false,
                IsRetweeted = false,
                ReplyCount = 0,
                FavoriteCount = 0,
                RetweetCount = 0,
                UserProfileImage = ImageCache.GetOrCreate(
                    "https://pbs.twimg.com/profile_images/1938605137813282816/u5D3g9W3_400x400.jpg",
                    96)
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
                System.Diagnostics.Debug.WriteLine($"GetNewTweetsAsync 開始 (type: {CurrentTimelineType})");

                var (tweetList, _) = await FetchTimelinePageAsync(null);
                if (tweetList == null) return newVms;

                var seenSet = GetSeenSet();
                var seenInResponse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dto in tweetList)
                {
                    var dedupKey = GetDedupKey(dto);
                    if (string.IsNullOrEmpty(dedupKey) || dedupKey == "error") continue;
                    if (seenSet.Contains(dedupKey) || seenInResponse.Contains(dedupKey)) continue;

                    seenInResponse.Add(dedupKey);
                    newVms.Add(CreateTweetViewModel(dto));
                }

                System.Diagnostics.Debug.WriteLine($"GetNewTweetsAsync 結果: {newVms.Count}件 (API取得後)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNewTweetsAsync Error: {ex.Message}");
            }

            return newVms;
        }
    }
}
