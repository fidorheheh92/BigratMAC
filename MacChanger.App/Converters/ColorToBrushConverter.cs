using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MacChanger.App.Converters;

/// <summary>Paints an accent swatch from the colour it represents.</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Color color) return Brushes.Transparent;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
