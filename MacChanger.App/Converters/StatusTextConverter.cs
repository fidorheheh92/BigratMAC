using System.Globalization;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Turns the operational status Windows reports into the two words a person
/// actually uses.
///
/// The status arrives as <see cref="System.Net.NetworkInformation.OperationalStatus"/>
/// text: "Up", "Down", "Dormant". Shown raw, "Up" reads as a direction rather
/// than as a connection. Nothing is invented: only states with an unambiguous
/// everyday name are translated, and anything else is shown as Windows named
/// it.
/// </summary>
public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.Trim();
        return status?.ToLowerInvariant() switch
        {
            "up" => "Connected",
            "down" => "Disconnected",
            "dormant" => "Dormant",
            "notpresent" => "Not present",
            "" or null => "Unknown",
            _ => status,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
