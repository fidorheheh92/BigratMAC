using System.Globalization;
using System.Windows.Data;

namespace MacChanger.App.Converters;

/// <summary>
/// Answers whether the item being templated is the first or the last in its
/// list, so a template can draw a divider *between* rows without knowing how
/// many rows there are.
///
/// WPF has no <c>:not(:last-child)</c>. The nearest equivalent is the
/// container's <see cref="System.Windows.Controls.ItemsControl.AlternationIndex"/>
/// pair with the item count, which is what this takes as a multi-binding:
///
///     <MultiBinding Converter="{StaticResource ItemPosition}" ConverterParameter="last">
///         <Binding Path="(ItemsControl.AlternationIndex)"
///                  RelativeSource="{RelativeSource AncestorType=ContentPresenter}"/>
///         <Binding Path="Items.Count"
///                  RelativeSource="{RelativeSource AncestorType=ItemsControl}"/>
///     </MultiBinding>
///
/// The ItemsControl must set AlternationCount; without it every container
/// reports index 0. Set it larger than any list this app shows (see the call
/// sites), because the index wraps at that number.
/// </summary>
public sealed class ItemPositionConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        if (values[0] is not int index || values[1] is not int count) return false;

        return string.Equals(parameter as string, "last", StringComparison.OrdinalIgnoreCase)
            ? index == count - 1
            : index == 0;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
