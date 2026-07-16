using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace App2
{
    internal static class TweetActionClient
    {
        private sealed class ActionResponse
        {
            public bool success { get; set; }
        }

        public static async Task<bool> LikeAsync(HttpClient client, string tweetId, bool currentlyLiked)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return false;
            }

            try
            {
                var response = currentlyLiked
                    ? await client.DeleteAsync($"http://localhost:8000/like/{tweetId}")
                    : await client.PostAsync($"http://localhost:8000/like/{tweetId}", null);
                return await IsActionSuccessfulAsync(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Like API Error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> RetweetAsync(HttpClient client, string tweetId)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return false;
            }

            try
            {
                var response = await client.PostAsync($"http://localhost:8000/retweet/{tweetId}", null);
                return await IsActionSuccessfulAsync(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Retweet API Error: {ex.Message}");
                return false;
            }
        }

        public static async Task<HttpResponseMessage> QuoteAsync(
            HttpClient client,
            string tweetId,
            string quoteText)
        {
            if (string.IsNullOrEmpty(tweetId))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }

            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(new { text = quoteText }),
                    Encoding.UTF8,
                    "application/json");
                return await client.PostAsync($"http://localhost:8000/quote/{tweetId}", content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Quote API Error: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
        }

        private static async Task<bool> IsActionSuccessfulAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                var result = JsonSerializer.Deserialize<ActionResponse>(json);
                return result?.success ?? true;
            }
            catch (JsonException)
            {
                return true;
            }
        }
    }
}
