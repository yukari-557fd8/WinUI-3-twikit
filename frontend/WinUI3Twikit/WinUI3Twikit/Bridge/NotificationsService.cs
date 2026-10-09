using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Twikit;
using Twikit.Api;

namespace WinUI3Twikit.Bridge
{
    /// <summary>
    /// 通知の取得（旧 <c>get_notifications_twikit.py</c>）。
    /// X の <c>notifications/all.json</c> と <c>notifications/mentions.json</c> の生の JSON を、WinUI 側（<c>NotificationDto</c>）の形に組み替える。
    /// </summary>
    internal static class NotificationsService
    {
        private static readonly object Gate = new();

        /// <summary>次ページ用。種別ごと。<c>refresh</c> はその種別だけ捨てる。</summary>
        private sealed class CursorState
        {
            public string? Cursor;
            public bool Exhausted;
        }

        private static readonly Dictionary<string, CursorState> Cursors = new(StringComparer.Ordinal);

        private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
        {
            ["Jan"] = 1,
            ["Feb"] = 2,
            ["Mar"] = 3,
            ["Apr"] = 4,
            ["May"] = 5,
            ["Jun"] = 6,
            ["Jul"] = 7,
            ["Aug"] = 8,
            ["Sep"] = 9,
            ["Oct"] = 10,
            ["Nov"] = 11,
            ["Dec"] = 12,
        };

        public static string NormalizeType(string? type)
            => string.Equals(type, "mentions", StringComparison.OrdinalIgnoreCase) ? "mentions" : "all";

        private static CursorState StateFor(string type)
        {
            if (!Cursors.TryGetValue(type, out var state))
            {
                state = new CursorState();
                Cursors[type] = state;
            }

            return state;
        }

        public static async Task<JsonArray> GetNotificationsAsync(int count, bool refresh, string? type = null, bool keepCursor = false)
        {
            var results = new JsonArray();
            var notificationType = NormalizeType(type);
            // 一覧を残したままの最新取得は、続き用カーソルを先頭ページのもので上書きしない。
            var preserveCursor = refresh && keepCursor;

            try
            {
                TwikitSession.Login();
                var client = TwikitSession.Client;

                string? cursor;
                lock (Gate)
                {
                    var state = StateFor(notificationType);
                    if (refresh)
                    {
                        if (!preserveCursor)
                        {
                            state.Cursor = null;
                            state.Exhausted = false;
                        }
                        cursor = null;
                        Debug.WriteLine($"✅ 通知({notificationType}): 最新から取得");
                    }
                    else
                    {
                        if (state.Exhausted || string.IsNullOrEmpty(state.Cursor))
                        {
                            Debug.WriteLine($"これ以上通知はありません ({notificationType})");
                            return results;
                        }

                        cursor = state.Cursor;
                        Debug.WriteLine($".cursor で追加取得 ({notificationType})");
                    }
                }

                var parameters = new Dictionary<string, string>
                {
                    ["count"] = count.ToString(CultureInfo.InvariantCulture),
                    ["include_ext_views"] = "true",
                    ["include_reply_count"] = "1",
                };
                if (cursor is not null)
                {
                    parameters["cursor"] = cursor;
                }

                var endpoint = notificationType == "mentions"
                    ? V11Endpoint.NotificationsMentions
                    : V11Endpoint.NotificationsAll;
                var response = await client.GetAsync(
                    endpoint,
                    new RequestOptions { Params = parameters, Headers = client.BaseHeaders }).ConfigureAwait(false);
                var json = response.Object ?? [];
                results = ItemsFromResponse(json, includeTimelineTweets: notificationType == "mentions");

                if (!preserveCursor)
                {
                    var nextCursor = BottomCursor(json);
                    lock (Gate)
                    {
                        var state = StateFor(notificationType);
                        if (nextCursor is not null && nextCursor != cursor)
                        {
                            state.Cursor = nextCursor;
                            state.Exhausted = false;
                        }
                        else
                        {
                            state.Cursor = null;
                            state.Exhausted = true;
                        }
                    }
                }

                if (results.Count == 0)
                {
                    Debug.WriteLine("通知はありません");
                    return results;
                }

                var replyCount = results.Count(item => item?["type"]?.GetValue<string>() == "reply");
                var quoteCount = results.Count(item => item?["type"]?.GetValue<string>() == "quote");
                Debug.WriteLine($"Notifications 取得 ({notificationType}): {results.Count} 件 (reply {replyCount}, quote {quoteCount})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notifications取得エラー ({notificationType}): {ex.Message}");
                if (!preserveCursor)
                {
                    lock (Gate)
                    {
                        var state = StateFor(notificationType);
                        state.Cursor = null;
                        state.Exhausted = false;
                    }
                }
            }

            return results;
        }

        internal static string? BottomCursor(JsonObject response)
        {
            foreach (var instruction in response.Sub("timeline").ArrOrEmpty("instructions").Objects())
            {
                foreach (var entry in instruction.Sub("addEntries").ArrOrEmpty("entries").Objects())
                {
                    if (!(entry.Str("entryId") ?? string.Empty).StartsWith("cursor-bottom", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var content = entry.Sub("content");
                    var value = content.Sub("operation").Sub("cursor").Str("value") ?? content.Str("value");
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        /// <summary>"Tue Sep 29 03:04:05 +0000 2026" をエポックミリ秒に。ロケールの strptime に依存しない。</summary>
        private static long TimestampMsFromTwitter(string createdAt)
        {
            var parts = (createdAt ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6 || !Months.TryGetValue(parts[1], out var month))
            {
                return 0;
            }

            try
            {
                var day = int.Parse(parts[2], CultureInfo.InvariantCulture);
                var time = parts[3].Split(':');
                var hour = int.Parse(time[0], CultureInfo.InvariantCulture);
                var minute = int.Parse(time[1], CultureInfo.InvariantCulture);
                var second = int.Parse(time[2], CultureInfo.InvariantCulture);
                var year = int.Parse(parts[5], CultureInfo.InvariantCulture);
                var tz = parts[4];
                var sign = tz.StartsWith('+') ? 1 : -1;
                var offset = new TimeSpan(
                    sign * int.Parse(tz.AsSpan(1, 2), CultureInfo.InvariantCulture),
                    sign * int.Parse(tz.AsSpan(3, 2), CultureInfo.InvariantCulture),
                    0);
                var dt = new DateTimeOffset(year, month, day, hour, minute, second, offset);
                return dt.ToUnixTimeMilliseconds();
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
            {
                return 0;
            }
        }

        private static string FormatTimestampMs(long timestampMs)
        {
            if (timestampMs == 0)
            {
                return "不明";
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs)
                .ToOffset(TweetSerializer.Jst)
                .ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static int ViewCount(JsonObject tweet)
        {
            foreach (var key in new[] { "ext_views", "views" })
            {
                if (tweet.Get(key) is JsonObject views)
                {
                    return TweetSerializer.MetricCount(views.Get("count").AsLong());
                }
            }

            return 0;
        }

        private static string TweetText(JsonObject tweet)
        {
            if (tweet.Count == 0)
            {
                return string.Empty;
            }

            var note = tweet.Sub("note_tweet").Sub("note_tweet_results").Sub("result").Str("text");
            return NonEmpty(note) ?? NonEmpty(tweet.Str("full_text")) ?? tweet.Str("text") ?? string.Empty;
        }

        private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

        private static JsonObject RawQuoteToDict(JsonObject tweet, JsonObject tweets, JsonObject users)
        {
            var quotedId = tweet.Str("quoted_status_id_str")
                ?? tweet.Str("quoted_status_id")
                ?? tweet.Sub("quoted_status").Str("id");
            var quoted = string.IsNullOrEmpty(quotedId)
                ? tweet.Sub("quoted_status")
                : tweets.Obj(quotedId) ?? tweet.Sub("quoted_status");

            if (quoted.Count == 0)
            {
                return new JsonObject { ["is_unavailable"] = true };
            }

            var user = users.Sub(quoted.Str("user_id_str") ?? quoted.Str("user_id") ?? string.Empty);
            var quotedText = TweetSerializer.NormalizeText(TweetText(quoted));
            quotedText = StripMediaUrls(quotedText, quoted);
            if (HasRawQuote(quoted))
            {
                quotedText = StripQuotedUrl(quotedText, quoted);
            }

            return new JsonObject
            {
                ["id"] = quoted.Str("id") ?? quotedId ?? string.Empty,
                ["text"] = quotedText,
                ["created_at"] = FormatTimestampMs(TimestampMsFromTwitter(quoted.Str("created_at") ?? string.Empty)),
                ["user_name"] = user.Str("name") ?? "Unknown",
                ["user_screen_name"] = user.Str("screen_name") ?? string.Empty,
                ["user_profile_image"] = user.Str("profile_image_url_https") ?? user.Str("profile_image_url") ?? string.Empty,
                ["user_protected"] = user.Get("protected").IsTruthy(),
                ["user_verified"] = UserVerified(user),
                ["media_items"] = TweetSerializer.ExtractMedia(quoted),
                ["is_unavailable"] = false,
            };
        }

        private static bool HasRawQuote(JsonObject tweet)
            => !string.IsNullOrEmpty(tweet.Str("quoted_status_id_str"))
               || !string.IsNullOrEmpty(tweet.Str("quoted_status_id"))
               || tweet.Sub("quoted_status").Count > 0;

        /// <summary>返信先として別行に出す @ユーザー名を、本文先頭から外す。</summary>
        private static string StripLeadingReplyMention(string text, string screenName)
        {
            var name = (screenName ?? string.Empty).Trim().TrimStart('@');
            if (string.IsNullOrEmpty(text) || name.Length == 0)
            {
                return text;
            }

            var prefix = "@" + name;
            if (text.Length < prefix.Length
                || !string.Equals(text[..prefix.Length], prefix, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var rest = text[prefix.Length..];
            if (rest.Length > 0 && rest[0] < 128 && (char.IsLetterOrDigit(rest[0]) || rest[0] == '_'))
            {
                return text;
            }

            return rest.TrimStart(' ', '\t', '\r', '\n', '　');
        }

        private static string ReplyToScreenName(JsonObject tweet, JsonObject users, JsonObject tweets)
        {
            var name = (tweet.Str("in_reply_to_screen_name") ?? string.Empty).Trim().TrimStart('@');
            if (name.Length > 0)
            {
                return name;
            }

            var userId = tweet.Str("in_reply_to_user_id_str") ?? tweet.Str("in_reply_to_user_id") ?? string.Empty;
            name = (users.Sub(userId).Str("screen_name") ?? string.Empty).Trim().TrimStart('@');
            if (name.Length > 0)
            {
                return name;
            }

            var parentId = tweet.Str("in_reply_to_status_id_str") ?? tweet.Str("in_reply_to_status_id") ?? string.Empty;
            var parent = tweets.Sub(parentId);
            var parentUserId = parent.Str("user_id_str") ?? parent.Str("user_id") ?? string.Empty;
            return (users.Sub(parentUserId).Str("screen_name") ?? string.Empty).Trim().TrimStart('@');
        }

        private static bool UserVerified(JsonObject user)
            => user.Get("verified").IsTruthy()
               || user.Get("is_blue_verified").IsTruthy()
               || user.Get("ext_is_blue_verified").IsTruthy();

        private static void AddActor(JsonObject item, JsonObject users, string? userId)
        {
            var user = string.IsNullOrEmpty(userId) ? [] : users.Sub(userId);
            if (user.Count == 0)
            {
                item["actor_name"] = "Unknown";
                item["actor_screen_name"] = string.Empty;
                item["actor_profile_image"] = string.Empty;
                return;
            }

            item["actor_name"] = NonEmpty(user.Str("name")) ?? "Unknown";
            item["actor_screen_name"] = user.Str("screen_name") ?? string.Empty;
            item["actor_profile_image"] = NonEmpty(user.Str("profile_image_url_https")) ?? user.Str("profile_image_url") ?? string.Empty;
        }

        /// <summary>
        /// X の通知 <c>icon</c> にある id。heart / retweet / person がいいね・リポスト・フォロー。
        /// 一致しなければ unknown のままにし、表示側が本文で判別する。
        /// </summary>
        private static string AggregateType(JsonObject notification)
        {
            var icon = notification.Obj("icon");
            if (icon is null)
            {
                return "unknown";
            }

            foreach (var kv in icon)
            {
                var fromIcon = TypeFromIconId(kv.Value.AsStr());
                if (fromIcon is not null)
                {
                    return fromIcon;
                }
            }

            return "unknown";
        }

        private static string? TypeFromIconId(string? iconId)
        {
            if (string.IsNullOrWhiteSpace(iconId))
            {
                return null;
            }

            return iconId.Trim().ToLowerInvariant() switch
            {
                "heart_icon" or "heart_plus_icon" => "like",
                "retweet_icon" or "repost_icon" => "retweet",
                "person_icon" => "follow",
                _ => null
            };
        }

        internal static JsonArray ItemsFromResponse(JsonObject response, bool includeTimelineTweets = false)
        {
            var globalObjects = response.Sub("globalObjects");
            var users = globalObjects.Sub("users");
            var tweets = globalObjects.Sub("tweets");
            var extracted = new List<(long TimestampMs, JsonObject Item)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kv in globalObjects.Sub("notifications"))
            {
                if (kv.Value is not JsonObject notification)
                {
                    continue;
                }

                var actions = notification.Sub("template").Sub("aggregateUserActionsV1");
                if (actions.Count == 0)
                {
                    continue;
                }

                var notificationId = notification.Str("id") ?? string.Empty;
                if (notificationId.Length == 0 || !seen.Add(notificationId))
                {
                    continue;
                }

                var fromUsers = actions.ArrOrEmpty("fromUsers");
                var userId = fromUsers.Count > 0 ? fromUsers[0].Sub("user").Str("id") : null;
                var targetObjects = actions.ArrOrEmpty("targetObjects");
                var targetId = targetObjects.Count > 0 ? targetObjects[0].Sub("tweet").Str("id") : null;
                var target = string.IsNullOrEmpty(targetId) ? [] : tweets.Sub(targetId);
                var timestampMs = notification.Get("timestampMs").AsLong() ?? 0;

                var item = new JsonObject
                {
                    ["id"] = notificationId,
                    ["type"] = AggregateType(notification),
                    ["text"] = TweetSerializer.NormalizeText(notification.Sub("message").Str("text") ?? string.Empty),
                    ["created_at"] = FormatTimestampMs(timestampMs),
                    ["target_tweet_text"] = StripMediaUrls(TweetSerializer.NormalizeText(TweetText(target)), target),
                };
                AddActor(item, users, userId);
                extracted.Add((timestampMs, item));
            }

            // リプライ・引用ツイート・メンションは timeline の tweet entry として届く。
            foreach (var instruction in response.Sub("timeline").ArrOrEmpty("instructions").Objects())
            {
                foreach (var entry in instruction.Sub("addEntries").ArrOrEmpty("entries").Objects())
                {
                    var item = entry.Sub("content").Sub("item");
                    var tweetId = item.Sub("content").Sub("tweet").Str("id") ?? string.Empty;
                    if (string.IsNullOrEmpty(tweetId))
                    {
                        continue;
                    }

                    var element = item.Sub("clientEventInfo").Str("element");
                    if (element == "user_replied_to_your_tweet")
                    {
                        AddTweetCard(extracted, seen, tweets, users, tweetId, "reply");
                    }
                    else if (element == "user_quoted_your_tweet")
                    {
                        AddTweetCard(extracted, seen, tweets, users, tweetId, "quote");
                    }
                    else if (element == "user_mentioned_you")
                    {
                        AddTweetCard(extracted, seen, tweets, users, tweetId, "mention");
                    }
                }
            }

            // @ツイートの旧形式やフォールバック（entryId が tweet-* の投稿そのもの）
            if (includeTimelineTweets)
            {
                foreach (var instruction in response.Sub("timeline").ArrOrEmpty("instructions").Objects())
                {
                    foreach (var entry in instruction.Sub("addEntries").ArrOrEmpty("entries").Objects())
                    {
                        var entryId = entry.Str("entryId") ?? string.Empty;
                        if (!entryId.StartsWith("tweet-", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var tweetId = entryId["tweet-".Length..];
                        var tweet = tweets.Obj(tweetId);
                        if (tweet is null || tweet.Count == 0)
                        {
                            continue;
                        }

                        AddTweetCard(extracted, seen, tweets, users, tweetId, IsDirectReply(tweet) ? "reply" : "mention");
                    }
                }
            }

            var array = new JsonArray();
            foreach (var (_, item) in extracted.OrderByDescending(e => e.TimestampMs))
            {
                array.Add(item);
            }

            return array;
        }

        private static bool IsDirectReply(JsonObject tweet)
        {
            if (!string.IsNullOrEmpty(tweet.Str("in_reply_to_status_id_str"))
                || !string.IsNullOrEmpty(tweet.Str("in_reply_to_status_id")))
            {
                return true;
            }

            var name = (tweet.Str("in_reply_to_screen_name") ?? string.Empty).Trim().TrimStart('@');
            return name.Length > 0;
        }

        private static string StripQuotedUrl(string text, JsonObject tweet)
        {
            var quotedId = tweet.Str("quoted_status_id_str") ?? tweet.Str("quoted_status_id");
            if (string.IsNullOrEmpty(quotedId) || string.IsNullOrEmpty(text))
            {
                return text;
            }

            foreach (var u in tweet.Sub("entities").ArrOrEmpty("urls").Objects())
            {
                var expanded = u.Str("expanded_url") ?? string.Empty;
                if (expanded.Contains($"/status/{quotedId}", StringComparison.OrdinalIgnoreCase))
                {
                    var shortUrl = u.Str("url");
                    if (!string.IsNullOrEmpty(shortUrl))
                    {
                        text = text.Replace(shortUrl, string.Empty);
                    }
                }
            }

            return text.Trim();
        }

        private static string StripMediaUrls(string text, JsonObject tweet)
            => TweetSerializer.StripTrailingMediaUrls(text, tweet).Trim();

        private static void AddTweetCard(
            List<(long TimestampMs, JsonObject Item)> extracted,
            HashSet<string> seen,
            JsonObject tweets,
            JsonObject users,
            string tweetId,
            string type)
        {
            if (tweetId.Length == 0 || seen.Contains(tweetId))
            {
                return;
            }

            var tweet = tweets.Obj(tweetId);
            if (tweet is null || tweet.Count == 0)
            {
                return;
            }

            seen.Add(tweetId);
            var isReply = string.Equals(type, "reply", StringComparison.Ordinal);
            var isQuote = string.Equals(type, "quote", StringComparison.Ordinal);
            var timestampMs = TimestampMsFromTwitter(tweet.Str("created_at") ?? string.Empty);
            var user = users.Sub(tweet.Str("user_id_str") ?? string.Empty);
            var replyTo = isReply ? ReplyToScreenName(tweet, users, tweets) : string.Empty;
            var text = TweetSerializer.NormalizeText(TweetText(tweet));
            if (isReply)
            {
                text = StripLeadingReplyMention(text, replyTo);
            }

            if (isQuote || HasRawQuote(tweet))
            {
                text = StripQuotedUrl(text, tweet);
            }

            text = StripMediaUrls(text, tweet);

            var card = new JsonObject
            {
                ["id"] = tweetId,
                ["type"] = type,
                ["text"] = text,
                ["created_at"] = FormatTimestampMs(timestampMs),
                ["target_tweet_text"] = string.Empty,
                ["reply_count"] = TweetSerializer.MetricCount(tweet.Get("reply_count").AsLong()),
                ["retweet_count"] = TweetSerializer.MetricCount(tweet.Get("retweet_count").AsLong()),
                ["favorite_count"] = TweetSerializer.MetricCount(tweet.Get("favorite_count").AsLong()),
                ["view_count"] = ViewCount(tweet),
                ["is_liked"] = tweet.Get("favorited").IsTruthy(),
                ["is_retweeted"] = tweet.Get("retweeted").IsTruthy(),
                ["user_protected"] = user.Get("protected").IsTruthy(),
                ["user_verified"] = UserVerified(user),
                ["reply_to_screen_name"] = replyTo,
                ["media_items"] = TweetSerializer.ExtractMedia(tweet),
            };
            if (HasRawQuote(tweet))
            {
                card["quoted_tweet"] = RawQuoteToDict(tweet, tweets, users);
            }
            AddActor(card, users, tweet.Str("user_id_str"));
            extracted.Add((timestampMs, card));
        }
    }
}
