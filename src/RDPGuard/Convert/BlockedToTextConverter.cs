using System.Globalization;
using System.Windows.Data;

namespace RDPGuard.Convert;

public class BlockedToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isBlocked)
        {
            var key = isBlocked ? "Str.Status.Blocked" : "Str.Status.Active";
            return Common.ResourceHelper.GetString(key);
        }
        return Common.ResourceHelper.GetString("Str.Status.Unknown");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
