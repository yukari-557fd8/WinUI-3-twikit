using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace App2
{
    public partial class RetweetColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isRetweeted && isRetweeted)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);  // リツイート済み = 緑
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);           // 未RT = 灰色
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
