using System.Globalization;

namespace YogaClassManager.Converters
{
    internal class TimeOnlyToTimeSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() != typeof(TimeOnly))
                return null;

            TimeOnly timeOnly = (TimeOnly)value;

            return timeOnly.ToTimeSpan();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() != typeof(TimeSpan))
                return null;

            TimeSpan timeSpan = (TimeSpan)value;

            return TimeOnly.FromTimeSpan(timeSpan);
        }
    }
}
