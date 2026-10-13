using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MacChanger.App.Converters;

/// <summary>
/// Maps a status word onto one of the palette's five status colours, so that
/// dots, chips and inline values all speak the same colour language.
///
/// Anything unrecognised falls back to the neutral grey rather than guessing
/// at a meaning. <see cref="StatusFamily"/> is shared with
/// <see cref="StatusSoftBrushConverter"/> so the two can never disagree about
/// which family a status belongs to.
/// </summary>
public sealed class StatusBrushConverter : IValueConverter
{
    static readonly Brush Fallback = Frozen(Color.FromRgb(0x8B, 0x90, 0x9A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Lookup("Brush." + StatusFamily(value?.ToString()));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>
    /// The palette family for a status word: Positive, Warning, Error, Muted
    /// or Neutral. Palette brushes are named "Brush.&lt;family&gt;" and the
    /// matching chip tint "Brush.&lt;family&gt;Soft".
    /// </summary>
    public static string StatusFamily(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "up" or "connected" or "ok" or "succeeded" or "success" => "Positive",
        "warning" or "degraded" or "limited" => "Warning",
        "error" or "failed" or "failure" => "Error",
        "disconnected" or "down" or "notpresent" => "Muted",
        _ => "Neutral",
    };

    static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    static Brush Lookup(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Fallback;
}
