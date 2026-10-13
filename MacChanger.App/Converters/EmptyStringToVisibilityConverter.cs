using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Collapses an element when the bound string is null, empty or whitespace.
///
/// The shell uses this so that status text only occupies space when there is
/// genuinely something to report — BigRat has no permanent "Ready" line.
///
/// Set <see cref="Invert"/> to show the element while the string is missing
/// instead, which is how a page explains an absent value in the place the value
/// would have been.
/// </summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var missing = string.IsNullOrWhiteSpace(value as string);
        if (Invert) missing = !missing;
        return missing ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
