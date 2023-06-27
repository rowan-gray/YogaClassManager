using System.Globalization;
using YogaClassManager.Models.Classes;

namespace YogaClassManager.Converters
{
    internal class IsTermCompletedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value.GetType() != typeof(Term))
                return null;

            var term = (Term)value;

            var now = DateOnly.FromDateTime(DateTime.Now);

            return term.EndDate < now && (term.CatchupEndDate is null || term.CatchupEndDate < now);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
