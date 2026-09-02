using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lab_Feedback_WPF.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // If it's a directory (true), hide the checkbox (Collapsed)
                // If it's a file (false), show the checkbox (Visible)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
