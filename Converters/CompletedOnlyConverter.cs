using System.Globalization;

namespace MAXTV.Converters;

public class CompletedOnlyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool invert = parameter?.ToString() == "invert";
        bool completed = value?.ToString() == "Completed";
        return invert ? !completed : completed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
