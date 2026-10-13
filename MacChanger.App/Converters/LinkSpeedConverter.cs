using System.Globalization;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Formats the adapter link speed reported by Windows (bits per second) as a
/// readable rate. A missing or zero speed is reported as unavailable rather
/// than shown as "0 Mbps".
/// </summary>
public sealed class LinkSpeedConverter : IValueConverter
{
    static readonly string[] Units = { "bps", "Kbps", "Mbps", "Gbps", "Tbps" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bits || bits <= 0) return "Unavailable";

        double rate = bits;
        var unit = 0;
        while (rate >= 1000 && unit < Units.Length - 1)
        {
            rate /= 1000;
            unit++;
        }

        return $"{rate.ToString("0.#", CultureInfo.InvariantCulture)} {Units[unit]}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
