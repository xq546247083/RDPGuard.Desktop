using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RDPGuard.Convert
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString()!.Equals(parameter.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }
            return Binding.DoNothing;
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility v && v != Visibility.Visible;
        }
    }

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
}
