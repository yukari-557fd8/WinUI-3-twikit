using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace App2
{
    public class TweetDto
    {
        public string? id { get; set; }
        public string? text { get; set; }
        public string? created_at { get; set; }
        public string? user_name { get; set; }
        public string? user_screen_name { get; set; }
        public string? user_profile_image { get; set; }
        public int favorite_count { get; set; }
        public int retweet_count { get; set; }
        public int reply_count { get; set; }
        public List<MediaItemDto>? media_items { get; set; }
    }

    public class MediaItemDto
    {
        public string? type { get; set; }
        public string? url { get; set; }
        public string? thumbnail { get; set; }
    }

    public class MediaItem
    {
        public string Type { get; set; } = "image";
        public string Url { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;
        public ImageSource? ThumbnailImage { get; set; }
    }

    public class TweetViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserScreenName { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public ImageSource? UserProfileImage { get; set; }
        public int FavoriteCount { get; set; } = 0;
        public int RetweetCount { get; set; } = 0;
        public int ReplyCount { get; set; } = 0;
        public List<MediaItem> MediaItems { get; set; } = new();

        public bool IsLiked { get; set; } = false;
        public bool IsRetweeted { get; set; } = false;

        private bool _isReplying = false;
        public bool IsReplying
        {
            get => _isReplying;
            set { _isReplying = value; OnPropertyChanged(nameof(IsReplying)); }
        }

        private string _replyText = string.Empty;
        public string ReplyText
        {
            get => _replyText;
            set { _replyText = value; OnPropertyChanged(nameof(ReplyText)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void ToggleLike()
        {
            IsLiked = !IsLiked;
            FavoriteCount += IsLiked ? 1 : -1;
            OnPropertyChanged(nameof(IsLiked));
            OnPropertyChanged(nameof(FavoriteCount));
        }

        public void ToggleRetweet()
        {
            IsRetweeted = !IsRetweeted;
            RetweetCount += IsRetweeted ? 1 : -1;
            OnPropertyChanged(nameof(IsRetweeted));
            OnPropertyChanged(nameof(RetweetCount));
        }
    }
}