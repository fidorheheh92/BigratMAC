using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacChanger.App.Converters;

/// <summary>
/// Maps a status word onto the matching pale, tinted surface from the palette.
/// Used by status chips, where the label takes the full-strength status colour
/// and the surface behind it takes the muted one.
/// </summary>
public sealed class StatusSoftBrushConverter : IValueConverter
{
    static readonly Brush Fallback = Frozen(Color.FromRgb(0x24, 0x27, 0x2C));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Lookup("Brush." + StatusBrushConverter.StatusFamily(value?.ToString()) + "Soft");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    static Brush Lookup(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Fallback;
}
