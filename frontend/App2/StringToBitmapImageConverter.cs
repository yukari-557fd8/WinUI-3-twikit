using Microsoft.UI.Xaml.Data;
using System;

namespace App2
{
    public sealed partial class StringToBitmapImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string imageUrl || string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            try
            {
                int decodePixelWidth = 0;

                if (parameter is string sizeParameter && !string.IsNullOrWhiteSpace(sizeParameter))
                {
                    var parts = sizeParameter.Split(',');
                    int targetLongEdge = 0;

                    if (parts.Length >= 1 && int.TryParse(parts[0], out var width) && width > 0)
                    {
                        targetLongEdge = width;
                    }

                    if (parts.Length >= 2 && int.TryParse(parts[1], out var height) && height > 0)
                    {
                        targetLongEdge = Math.Max(targetLongEdge, height);
                    }

                    decodePixelWidth = targetLongEdge;
                }

                return ImageCache.GetOrCreate(imageUrl, decodePixelWidth);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
