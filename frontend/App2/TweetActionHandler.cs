using System;
using System.Threading.Tasks;

namespace App2
{
    internal static class TweetActionHandler
    {
        public static async Task HandleLikeAsync(
            TweetViewModel vm,
            Func<string, bool, Task<bool>> executeLike,
            Func<string, TweetViewModel?> findTweet)
        {
            if (string.IsNullOrEmpty(vm.Id) || !TweetActionRequestGuard.TryBeginLike(vm.Id))
            {
                return;
            }

            var target = findTweet(vm.Id) ?? vm;
            bool wasLiked = target.IsLiked;
            target.ToggleLike();

            try
            {
                if (!await executeLike(vm.Id, wasLiked))
                {
                    (findTweet(vm.Id) ?? target).ToggleLike();
                }
            }
            catch (Exception ex)
            {
                (findTweet(vm.Id) ?? target).ToggleLike();
                System.Diagnostics.Debug.WriteLine($"Like handler error: {ex.Message}");
            }
            finally
            {
                TweetActionRequestGuard.EndLike(vm.Id);
            }
        }

        public static async Task HandleRetweetAsync(
            TweetViewModel vm,
            Func<string, Task<bool>> executeRetweet,
            Func<string, TweetViewModel?> findTweet)
        {
            if (string.IsNullOrEmpty(vm.Id) || !TweetActionRequestGuard.TryBeginRetweet(vm.Id))
            {
                return;
            }

            var target = findTweet(vm.Id) ?? vm;
            target.ToggleRetweet();

            try
            {
                if (!await executeRetweet(vm.Id))
                {
                    (findTweet(vm.Id) ?? target).ToggleRetweet();
                }
            }
            catch (Exception ex)
            {
                (findTweet(vm.Id) ?? target).ToggleRetweet();
                System.Diagnostics.Debug.WriteLine($"Retweet handler error: {ex.Message}");
            }
            finally
            {
                TweetActionRequestGuard.EndRetweet(vm.Id);
            }
        }
    }
}
