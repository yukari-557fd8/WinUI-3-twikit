using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace App2
{
    public class ListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int SubscriberCount { get; set; }
        public string MemberInfo => $"{MemberCount} メンバー · {SubscriberCount} 購読者";
    }

    public partial class ListsViewModel : INotifyPropertyChanged
    {
        private const int DefaultListFetchCount = 100;
        private const int DefaultTweetFetchCount = 30;

        public ObservableCollection<ListItemViewModel> Lists { get; } = [];
        public ObservableCollection<TweetViewModel> Tweets { get; } = [];

        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _seenTweetIds = new(StringComparer.OrdinalIgnoreCase);
        private string? _nextTweetCursor;
        private string? _selectedListId;
        private string _selectedListName = string.Empty;
        private double _scrollVerticalOffset;

        private bool _isLoading;
        private bool _isLoadingMore;
        private bool _showTimeline;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(IsListCatalogContentVisible));
                    OnPropertyChanged(nameof(IsTimelineContentVisible));
                }
            }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set
            {
                if (_isLoadingMore != value)
                {
                    _isLoadingMore = value;
                    OnPropertyChanged(nameof(IsLoadingMore));
                }
            }
        }

        public bool ShowTimeline
        {
            get => _showTimeline;
            set
            {
                if (_showTimeline != value)
                {
                    _showTimeline = value;
                    OnPropertyChanged(nameof(ShowTimeline));
                    OnPropertyChanged(nameof(ShowListCatalog));
                    OnPropertyChanged(nameof(IsListCatalogContentVisible));
                    OnPropertyChanged(nameof(IsTimelineContentVisible));
                }
            }
        }

        public bool ShowListCatalog => !ShowTimeline;

        public bool IsListCatalogContentVisible => ShowListCatalog && !IsLoading;

        public bool IsTimelineContentVisible => ShowTimeline && !IsLoading;

        public string SelectedListName
        {
            get => _selectedListName;
            private set
            {
                if (_selectedListName != value)
                {
                    _selectedListName = value;
                    OnPropertyChanged(nameof(SelectedListName));
                }
            }
        }

        public double ScrollVerticalOffset
        {
            get => _scrollVerticalOffset;
            set => _scrollVerticalOffset = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public async Task LoadListsAsync()
        {
            IsLoading = true;
            Lists.Clear();

            try
            {
                var url = $"http://localhost:8000/lists?count={DefaultListFetchCount}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<ListsApiResponse>(json);

                if (payload?.lists != null)
                {
                    foreach (var dto in payload.lists)
                    {
                        Lists.Add(new ListItemViewModel
                        {
                            Id = dto.id ?? string.Empty,
                            Name = dto.name ?? string.Empty,
                            Description = dto.description ?? string.Empty,
                            Mode = dto.mode ?? string.Empty,
                            MemberCount = dto.member_count,
                            SubscriberCount = dto.subscriber_count,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadLists Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task SelectListAsync(string listId, string listName)
        {
            _selectedListId = listId;
            SelectedListName = listName;
            ShowTimeline = true;

            IsLoading = true;
            Tweets.Clear();
            _seenTweetIds.Clear();
            _nextTweetCursor = null;

            try
            {
                await LoadMoreTweetsAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void BackToListCatalog()
        {
            ShowTimeline = false;
            _selectedListId = null;
            SelectedListName = string.Empty;
            Tweets.Clear();
            _seenTweetIds.Clear();
            _nextTweetCursor = null;
            ScrollVerticalOffset = 0;
        }

        public async Task LoadMoreTweetsAsync()
        {
            if (IsLoadingMore || string.IsNullOrEmpty(_selectedListId))
            {
                return;
            }

            IsLoadingMore = true;

            try
            {
                var url =
                    $"http://localhost:8000/lists/{Uri.EscapeDataString(_selectedListId)}/tweets?count={DefaultTweetFetchCount}";
                if (!string.IsNullOrWhiteSpace(_nextTweetCursor))
                {
                    url += $"&cursor={Uri.EscapeDataString(_nextTweetCursor)}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<ListTimelineApiResponse>(json);
                _nextTweetCursor = payload?.next_cursor;

                if (payload?.tweets != null)
                {
                    foreach (var dto in payload.tweets)
                    {
                        if (string.IsNullOrEmpty(dto.id) || _seenTweetIds.Contains(dto.id))
                        {
                            continue;
                        }

                        _seenTweetIds.Add(dto.id);
                        Tweets.Add(CreateTweetViewModel(dto));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMoreTweets Error: {ex.Message}");
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private static TweetViewModel CreateTweetViewModel(TweetDto dto) => TweetViewModel.FromDto(dto);

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

            var index = Tweets.IndexOf(originalVm);
            if (index >= 0)
            {
                Tweets.Insert(index + 1, replyVm);
            }
            else
            {
                Tweets.Add(replyVm);
            }
        }

        private sealed class ListsApiResponse
        {
            public List<ListDto>? lists { get; set; }
            public string? next_cursor { get; set; }
        }

        private sealed class ListDto
        {
            public string? id { get; set; }
            public string? name { get; set; }
            public string? description { get; set; }
            public string? mode { get; set; }
            public int member_count { get; set; }
            public int subscriber_count { get; set; }
        }

        private sealed class ListTimelineApiResponse
        {
            public List<TweetDto>? tweets { get; set; }
            public string? next_cursor { get; set; }
        }
    }
}
