using System;
using System.Collections.Generic;

namespace App2
{
    internal static class TweetActionRequestGuard
    {
        private static readonly HashSet<string> PendingLikes = new(StringComparer.Ordinal);
        private static readonly HashSet<string> PendingRetweets = new(StringComparer.Ordinal);
        private static readonly object Sync = new();

        public static bool TryBeginLike(string tweetId)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return false;
            }

            lock (Sync)
            {
                return PendingLikes.Add(tweetId);
            }
        }

        public static void EndLike(string tweetId)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return;
            }

            lock (Sync)
            {
                PendingLikes.Remove(tweetId);
            }
        }

        public static bool TryBeginRetweet(string tweetId)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return false;
            }

            lock (Sync)
            {
                return PendingRetweets.Add(tweetId);
            }
        }

        public static void EndRetweet(string tweetId)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return;
            }

            lock (Sync)
            {
                PendingRetweets.Remove(tweetId);
            }
        }
    }
}
