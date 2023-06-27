using System.Globalization;

namespace YogaClassManager.Converters
{
    internal class DateOnlyToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value.GetType() != typeof(DateOnly))
                return null;

            DateOnly dateOnly = (DateOnly)value;

            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value.GetType() != typeof(DateTime))
                return null;

            DateTime dateTime = (DateTime)value;

            return DateOnly.FromDateTime(dateTime);
        }
    }
}
