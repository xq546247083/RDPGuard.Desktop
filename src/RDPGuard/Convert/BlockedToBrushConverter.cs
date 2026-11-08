using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RDPGuard.Convert;

public class BlockedToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isBlocked)
        {
            var key = isBlocked ? "Brush.Status.Danger" : "Brush.Status.Success";
            return Application.Current?.TryFindResource(key) as Brush ?? (isBlocked ? Brushes.Crimson : Brushes.ForestGreen);
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
