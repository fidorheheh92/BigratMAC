using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacChanger.App.Converters;

/// <summary>
/// Picks the icon for a hardware section from its title.
///
/// The titles come from <c>WindowsHardwareInfoService</c> and are fixed
/// strings, so this is a plain lookup rather than anything clever. A title that
/// is not recognised gets the generic information glyph — never a wrong icon,
/// and never a blank space.
/// </summary>
public sealed class SectionIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Lookup(value?.ToString()?.Trim() switch
        {
            "System" => "Icon.Computer",
            "System UUID" => "Icon.Shield",
            "Processor" => "Icon.Speed",
            "Motherboard" => "Icon.Board",
            "BIOS" => "Icon.Firmware",
            "Memory modules" => "Icon.Memory",
            "Storage devices" => "Icon.Disk",
            "Graphics" => "Icon.Gpu",
            "Operating system" => "Icon.Os",
            _ => "Icon.Info",
        });

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    static Geometry? Lookup(string key) => Application.Current?.TryFindResource(key) as Geometry;
}
