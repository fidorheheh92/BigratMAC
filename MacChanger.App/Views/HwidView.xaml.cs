using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Read-only hardware identification. Everything shown here is queried from
/// Windows on demand; the page offers no way to alter any of it.
/// </summary>
public partial class HwidView : UserControl
{
    readonly AppState state;
    bool pulsing;

    public HwidView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.IsReadingHardware)) UpdateSkeletonPulse();
        };

        Loaded += async (_, _) =>
        {
            // Read once on first visit; "Read again" refreshes on demand.
            if (state.HardwareSections.Count == 0) await state.LoadHardwareInfoAsync();
        };
    }

    /// <summary>
    /// The mockup's 1.5s skeleton pulse. Opacity only, and started and stopped
    /// from the real read state rather than left running behind a hidden card.
    /// Reduced-motion users get still bars at a readable opacity.
    /// </summary>
    void UpdateSkeletonPulse()
    {
        if (state.IsReadingHardware)
        {
            if (pulsing) return;
            pulsing = true;

            Border[] bars = { SkeletonBar1, SkeletonBar2, SkeletonBar3 };

            if (!SystemParameters.ClientAreaAnimation)
            {
                foreach (var bar in bars) bar.Opacity = 0.7;
                return;
            }

            var pulse = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(1.5),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(1)));

            foreach (var bar in bars) bar.BeginAnimation(UIElement.OpacityProperty, pulse);
            return;
        }

        if (!pulsing) return;
        pulsing = false;

        Border[] done = { SkeletonBar1, SkeletonBar2, SkeletonBar3 };
        foreach (var bar in done)
        {
            bar.BeginAnimation(UIElement.OpacityProperty, null);
            bar.Opacity = 1;
        }
    }

    async void ReloadClick(object sender, RoutedEventArgs e)
    {
        await state.LoadHardwareInfoAsync();

        // Report what the read actually returned, not that a button was pressed.
        if (state.HardwareSections.Count > 0)
        {
            state.ShowToast(state.HardwareSections.Count == 1
                ? "1 hardware section read."
                : $"{state.HardwareSections.Count} hardware sections read.");
            return;
        }

        state.ShowToast(
            string.IsNullOrWhiteSpace(state.HardwareStatus)
                ? "No hardware information was reported."
                : state.HardwareStatus,
            true);
    }
}
