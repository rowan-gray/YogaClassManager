using System.Globalization;

namespace YogaClassManager.Converters
{
    internal class IntSubractionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return null;
            }
            if (parameter is null)
            {
                parameter = 0;
            }

            if (value.GetType() != typeof(int))
            {
                return null;
            }

            try
            {
                var subtraction = int.Parse((string)parameter);
                return (int)value - subtraction;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return null;
            }
            if (parameter is null)
            {
                parameter = 0;
            }

            if (value.GetType() != typeof(int))
            {
                return null;
            }
            if (parameter.GetType() != typeof(int))
            {
                return null;
            }

            return (int)value + (int)parameter;
        }
    }
}
