using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Landing page: the adapter Windows reports, a live reading of what it is
/// carrying, and the most recent operations from the history file.
/// </summary>
public partial class DashboardView : UserControl
{
    readonly AppState state;
    bool pulseRunning;

    public DashboardView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;

        // The card shows whether a figure has actually been measured — "Live",
        // "Measuring" or "No counters" — rather than assuming every adapter
        // reports counters, because not every driver does.
        state.Connection.PropertyChanged += (_, _) => UpdateConnectionState();
        UpdateConnectionState();
    }

    void UpdateConnectionState()
    {
        var live = state.Connection.HasReading;

        if (live)
        {
            ConnState.Text = "Live";
            ConnDot.Fill = (Brush)FindResource("Brush.Positive");
        }
        else if (state.Connection.IsUnavailable)
        {
            ConnState.Text = "No counters";
            ConnDot.Fill = (Brush)FindResource("Brush.Warning");
        }
        else
        {
            ConnState.Text = "Measuring";
            ConnDot.Fill = (Brush)FindResource("Brush.TextTertiary");
        }

        // The ring only belongs on a live reading: a pulse beside "Measuring"
        // would be claiming a measurement that has not happened yet. The
        // animation is started on the transition rather than on every sample,
        // because the monitor raises a change once a second and restarting the
        // 2.4s storyboard each time would leave it permanently at frame zero.
        if (live && !pulseRunning) StartPulse();
        else if (!live && pulseRunning) StopPulse();
    }

    /// <summary>
    /// The mockup's 2.4s pulse: scale 0.7 to 1.5 with the ring fading from
    /// 0.55 to nothing by 70% of the cycle, then holding at nothing.
    /// Reduced-motion users get the dot without the ring.
    /// </summary>
    void StartPulse()
    {
        pulseRunning = true;

        if (!SystemParameters.ClientAreaAnimation)
        {
            ConnPulse.Visibility = Visibility.Collapsed;
            return;
        }

        ConnPulse.Visibility = Visibility.Visible;

        var duration = TimeSpan.FromSeconds(2.4);
        var repeat = RepeatBehavior.Forever;

        var scale = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = repeat };
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(0.7, KeyTime.FromPercent(0)));
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(1.5, KeyTime.FromPercent(0.7)));
        scale.KeyFrames.Add(new LinearDoubleKeyFrame(1.5, KeyTime.FromPercent(1)));

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration, RepeatBehavior = repeat };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0.7)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        ConnPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        ConnPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        ConnPulse.BeginAnimation(OpacityProperty, fade);
    }

    void StopPulse()
    {
        pulseRunning = false;

        ConnPulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ConnPulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ConnPulse.BeginAnimation(OpacityProperty, null);
        ConnPulse.Opacity = 0;
        ConnPulse.Visibility = Visibility.Collapsed;
    }

    void CopyAddressClick(object sender, RoutedEventArgs e)
    {
        var address = state.SelectedAdapter?.CurrentMac;
        if (string.IsNullOrWhiteSpace(address)) return;

        try
        {
            Clipboard.SetText(address);
            state.ShowToast($"Copied {address}.");
        }
        catch
        {
            // The clipboard is a shared system resource and can genuinely be
            // locked by another process. Saying it failed is the honest answer;
            // silently doing nothing would look like a dead button.
            state.ShowToast("The clipboard is not available right now.", true);
        }
    }

    void NavigateClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page }) state.Navigate?.Invoke(page);
    }
}
