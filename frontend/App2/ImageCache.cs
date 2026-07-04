using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;

namespace App2
{
    public static class ImageCache
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static BitmapImage? GetOrCreate(string? imageUrl, int decodePixelWidth = 0)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            var key = decodePixelWidth > 0 ? $"{imageUrl}|w{decodePixelWidth}" : imageUrl;
            return _cache.GetOrAdd(key, _ => CreateBitmap(imageUrl, decodePixelWidth));
        }

        private static BitmapImage CreateBitmap(string imageUrl, int decodePixelWidth)
        {
            var bitmap = new BitmapImage(new Uri(imageUrl));
            if (decodePixelWidth > 0)
            {
                bitmap.DecodePixelWidth = decodePixelWidth;
            }

            return bitmap;
        }
    }
}