using System.Windows;
using System.Windows.Media;

namespace MacChanger.App.Services;

/// <summary>One selectable primary colour.</summary>
public sealed record AccentOption(string Name, Color Color);

/// <summary>
/// Swaps the application's primary colour at runtime by replacing the
/// Brush.Accent resources. Because every accent-driven brush in the UI is a
/// DynamicResource, one swap re-colours the whole application — no control
/// needs to be edited individually.
/// </summary>
public static class AccentPalette
{
    public const string Default = "Lime";

    /// <summary>
    /// The card surface the pale accent tints are mixed against. Kept in step
    /// with Brush.Card in Palette.xaml; it is here rather than looked up at
    /// runtime so that applying an accent never depends on resource lookup
    /// order.
    /// </summary>
    static readonly Color CardSurface = Color.FromRgb(0x19, 0x1B, 0x1F);

    public static IReadOnlyList<AccentOption> Options { get; } = new[]
    {
        new AccentOption("Lime",   Color.FromRgb(0xC1, 0xFF, 0x72)),
        new AccentOption("Cyan",   Color.FromRgb(0x5F, 0xD3, 0xE8)),
        new AccentOption("Violet", Color.FromRgb(0xB0, 0x8C, 0xFF)),
        new AccentOption("Amber",  Color.FromRgb(0xFF, 0xC2, 0x4B)),
    };

    public static AccentOption Find(string? name) =>
        Options.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Options[0];

    /// <summary>
    /// Applies an accent by name. Unknown names fall back to the default.
    ///
    /// The pale tints are derived here rather than shipped as one fixed colour
    /// per accent, so the selected navigation row and the other quiet accent
    /// surfaces stay in step with whatever the user picks. All four options are
    /// light colours, so the dark ink on top of them never has to change.
    /// </summary>
    public static void Apply(string? name)
    {
        var color = Find(name).Color;

        var accent = Frozen(color);
        var soft = Frozen(Over(color, CardSurface, 0.12));
        var softHover = Frozen(Over(color, CardSurface, 0.20));

        var resources = Application.Current?.Resources;
        if (resources is null) return;

        resources["Brush.Accent"] = accent;
        resources["Brush.AccentText"] = accent;
        resources["Brush.AccentSoft"] = soft;
        resources["Brush.AccentSoftHover"] = softHover;
    }

    /// <summary>Mixes <paramref name="top"/> over <paramref name="bottom"/> in the given proportion.</summary>
    static Color Over(Color top, Color bottom, double amount) => Color.FromRgb(
        (byte)Math.Round(bottom.R + (top.R - bottom.R) * amount),
        (byte)Math.Round(bottom.G + (top.G - bottom.G) * amount),
        (byte)Math.Round(bottom.B + (top.B - bottom.B) * amount));

    static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
