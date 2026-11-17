using System.Globalization;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Turns the enum-style interface names reported by Windows into the short
/// product copy used by the mockup without changing the underlying model.
/// </summary>
public sealed class InterfaceTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString()?.Trim() switch
        {
            "Wireless80211" => "Wi-Fi",
            { Length: > 0 } name => name,
            _ => "—",
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
