using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using MacChanger.Core;

namespace MacChanger.App.Services;

/// <summary>
/// Watches how much traffic is crossing the selected adapter, and keeps the
/// last minute of it for the Connection card's sparkline.
///
/// The figure is measured, not estimated: Windows keeps a running byte total
/// per interface, and this subtracts one reading from the next and divides by
/// the time between them. Nothing is sent anywhere to obtain it, which is why
/// the card can say so.
///
/// It samples for as long as the application is open rather than only while
/// the Dashboard is on screen. The card's axis is labelled in seconds, and a
/// window that only advanced while someone was looking at it would make that
/// label untrue.
/// </summary>
public sealed class ConnectionMonitor : INotifyPropertyChanged
{
    /// <summary>
    /// How many seconds of history the sparkline shows. The newest sample is
    /// "now" and the oldest is this many seconds before it, so the buffer
    /// holds one more sample than the window is wide.
    /// </summary>
    public const int WindowSeconds = 60;

    // One sample a second. This is the number the axis label counts out, so it
    // is a claim about the reading rather than a refresh rate.
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    readonly DispatcherTimer timer;
    readonly List<double> history = new();

    // Replaced rather than mutated. A binding cannot see inside a list that
    // changed, and the sparkline is told to repaint by the property changing,
    // so each sample publishes a fresh snapshot for it to draw.
    double[] snapshot = Array.Empty<double>();

    ThroughputCounters? previous;
    long previousTimestamp;
    string? trackedId;
    double down;
    double up;
    bool hasReading;
    bool unavailable;

    public ConnectionMonitor()
    {
        // Background priority: a reading that is one frame late is worth less
        // than a button that responds on time.
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        timer.Tick += (_, _) => Sample();
    }

    // ------------------------------------------------------------- lifecycle

    /// <summary>Begins sampling. Safe to call when it is already running.</summary>
    public void Start()
    {
        if (timer.IsEnabled) return;

        // Nothing is carried over from a previous run. The counters are
        // cumulative, so a window spent not sampling would otherwise be
        // reported as one enormous reading the moment sampling resumed.
        previous = null;
        timer.Start();
    }

    /// <summary>Stops sampling and forgets the baseline it was subtracting from.</summary>
    public void Stop()
    {
        timer.Stop();
        previous = null;
    }

    /// <summary>
    /// Points the monitor at an adapter, or at nothing.
    ///
    /// Changing adapter clears the history, because the trend it describes
    /// belongs to one adapter. Carried across a selection change it would draw
    /// sixty seconds that never happened on either of them.
    /// </summary>
    public void Track(NetworkAdapter? adapter)
    {
        var id = adapter?.Id;
        if (string.Equals(id, trackedId, StringComparison.Ordinal)) return;

        trackedId = id;
        previous = null;
        history.Clear();
        snapshot = Array.Empty<double>();
        down = 0;
        up = 0;
        hasReading = false;
        unavailable = false;
        RaiseAll();
    }

    // ------------------------------------------------------------ the reading

    /// <summary>True once a rate has actually been measured. Until then the card shows no figure.</summary>
    public bool HasReading => hasReading;

    /// <summary>
    /// True when Windows would not report counters for this adapter at all.
    ///
    /// Kept apart from <see cref="HasReading"/> being false, because the two
    /// mean different things to the reader: the first second of sampling is
    /// simply too early to say anything, while this is the card reporting that
    /// there is no figure to be had. A card that said "measuring" for both
    /// would be claiming an operation that is not happening.
    /// </summary>
    public bool IsUnavailable => unavailable && !hasReading;

    /// <summary>
    /// The ordinary state for the first second: sampling has started and the
    /// baseline reading has been taken, and there is simply nothing to report
    /// until the next one arrives.
    /// </summary>
    public bool IsWaiting => !hasReading && !unavailable;

    /// <summary>Download rate over the last interval, in megabits per second.</summary>
    public double DownMbps => down;

    /// <summary>Upload rate over the last interval, in megabits per second.</summary>
    public double UpMbps => up;

    /// <summary>The download figure as it is displayed. An em dash while there is nothing measured.</summary>
    public string DownText => hasReading ? Format(down) : "—";

    /// <summary>The upload figure as it is displayed.</summary>
    public string UpText => hasReading ? Format(up) : "—";

    /// <summary>The download history, oldest first, for the sparkline to draw.</summary>
    public IReadOnlyList<double> Series => snapshot;

    /// <summary>True once there are at least two samples, which is the fewest a line can be drawn from.</summary>
    public bool HasSeries => snapshot.Length > 1;

    /// <summary>
    /// The age of the oldest sample on the sparkline, for the axis label.
    ///
    /// It counts up to the full window rather than always claiming sixty
    /// seconds, so a line that has only been running for eight seconds says so.
    /// </summary>
    public string WindowLabel => $"{(snapshot.Length > 0 ? snapshot.Length - 1 : 0)} s ago";

    /// <summary>
    /// Rates are shown to three significant figures at most and fewer as they
    /// grow: nobody reads the hundredths of a 400 Mbps transfer, and at a
    /// trickle those hundredths are the only part that is moving.
    /// </summary>
    static string Format(double mbps)
    {
        if (mbps >= 100) return mbps.ToString("0", CultureInfo.InvariantCulture);
        if (mbps >= 10) return mbps.ToString("0.0", CultureInfo.InvariantCulture);
        return mbps.ToString("0.00", CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------- sampling

    void Sample()
    {
        if (trackedId is null) return;

        var now = Stopwatch.GetTimestamp();
        var counters = NetworkThroughput.Read(trackedId);

        if (counters is null)
        {
            // The interface went away underneath us — an adapter mid-restart,
            // most likely. Drop the baseline so the next reading that does
            // arrive is measured from itself and not from a stale total.
            previous = null;
            if (!unavailable)
            {
                unavailable = true;
                RaiseAll();
            }
            return;
        }

        if (unavailable)
        {
            unavailable = false;
            RaiseAll();
        }

        // The first reading after starting or switching has nothing to be
        // subtracted from. It becomes the baseline, and the rate is reported
        // from the next one onwards. Reporting it now would present the whole
        // of the counter since boot as one second of traffic.
        if (previous is null)
        {
            previous = counters;
            previousTimestamp = now;
            return;
        }

        var seconds = (now - previousTimestamp) / (double)Stopwatch.Frequency;
        if (seconds <= 0) return;

        var delta = counters.Value.Since(previous.Value);
        previous = counters;
        previousTimestamp = now;

        down = delta.Received * 8.0 / seconds / 1_000_000.0;
        up = delta.Sent * 8.0 / seconds / 1_000_000.0;
        hasReading = true;

        history.Add(down);
        if (history.Count > WindowSeconds + 1) history.RemoveAt(0);
        snapshot = history.ToArray();

        RaiseAll();
    }

    void RaiseAll()
    {
        OnPropertyChanged(nameof(HasReading));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(DownMbps));
        OnPropertyChanged(nameof(UpMbps));
        OnPropertyChanged(nameof(DownText));
        OnPropertyChanged(nameof(UpText));
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(HasSeries));
        OnPropertyChanged(nameof(WindowLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
