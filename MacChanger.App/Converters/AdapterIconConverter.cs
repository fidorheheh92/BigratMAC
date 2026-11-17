using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacChanger.App.Converters;

/// <summary>
/// Picks the badge glyph for an adapter from the interface type Windows
/// reports.
///
/// The values are <see cref="System.Net.NetworkInformation.NetworkInterfaceType"/>
/// names as written by the discovery service. Anything that is not wireless or
/// wired gets the generic information glyph rather than an icon that would
/// claim the adapter is something it is not.
/// </summary>
public sealed class AdapterIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Lookup(value?.ToString()?.Trim() switch
        {
            "Wireless80211" => "Icon.Wifi",
            "Ethernet" or "GigabitEthernet" or "FastEthernet"
                or "Ethernet3Megabit" or "Fddi" or "TokenRing" => "Icon.Plug",
            _ => "Icon.Info",
        });

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    static Geometry? Lookup(string key) => Application.Current?.TryFindResource(key) as Geometry;
}
