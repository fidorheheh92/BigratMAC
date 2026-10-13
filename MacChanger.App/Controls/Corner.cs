using System.Windows;

namespace MacChanger.App.Controls;

/// <summary>
/// Lets a style override the corner radius that is baked into a
/// <see cref="System.Windows.Controls.ControlTemplate"/> it inherits.
///
/// Normally a derived style can restyle everything about a control except the
/// shape drawn inside the template: <c>Button.Base</c> draws a
/// <c>Border</c> with <c>CornerRadius="{StaticResource Radius.Control}"</c>, and
/// <c>Button.Row</c> inherits that template, so it can only ever be as round as
/// its ancestor. The alternatives are worse — duplicating the whole template
/// per variant, or reaching for <c>ButtonChrome</c> and re-deriving it — so the
/// radius is lifted onto an attached property instead. The template reads it
/// back through the templated parent, and a derived style sets it like any
/// other property.
///
/// Registered on <see cref="DependencyObject"/> rather than on
/// <c>Control</c> so a template author can also set it on a panel or a border
/// if a future control needs the same treatment.
/// </summary>
public static class Corner
{
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.RegisterAttached(
            "Radius",
            typeof(CornerRadius),
            typeof(Corner),
            new FrameworkPropertyMetadata(default(CornerRadius)));

    public static CornerRadius GetRadius(DependencyObject element) =>
        (CornerRadius)element.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject element, CornerRadius value) =>
        element.SetValue(RadiusProperty, value);
}
