using System;
using System.Globalization;
using System.Windows.Data;

namespace ChatClient.MVVM.View
{
    public class ProgressBarWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 256; // Default width

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int progress)
            {
                return (progress / 100.0) * MaxWidth;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
