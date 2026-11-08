using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RDPGuard.Convert;

public class ResultToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSuccess)
        {
            var key = isSuccess ? "Brush.Status.Success" : "Brush.Status.Danger";
            return Application.Current?.TryFindResource(key) as Brush ?? (isSuccess ? Brushes.ForestGreen : Brushes.Crimson);
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
