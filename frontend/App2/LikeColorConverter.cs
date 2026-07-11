using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace App2
{
    public partial class LikeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isLiked && isLiked)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Red);  // いいね済み = 赤
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);     // 未いいね = 灰色
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
