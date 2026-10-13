using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Turns a boolean into <see cref="Visibility"/>. Set <see cref="Invert"/> to
/// true to show the element while the value is false — used to swap a list for
/// its empty-state message.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
