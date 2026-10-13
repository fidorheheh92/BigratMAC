using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace MacChanger.Core;

/// <summary>
/// One interface's own byte counters at one instant.
///
/// These are cumulative totals since the adapter was brought up, not rates.
/// The traffic that crossed the adapter between two readings is the difference
/// between them, divided by the time in between — which is why reading a
/// throughput figure takes two samples and a clock, and why a single reading
/// on its own means nothing.
/// </summary>
public readonly record struct ThroughputCounters(long Received, long Sent)
{
    /// <summary>
    /// The bytes moved since <paramref name="earlier"/>.
    ///
    /// A negative delta is reported as zero rather than as itself. Windows
    /// resets these counters when an adapter is disabled and re-enabled, which
    /// is something BigRat does on purpose after every MAC change, so a reset
    /// is an ordinary event here and not an error. Left unclamped it would
    /// appear on screen as a large negative throughput.
    /// </summary>
    public ThroughputCounters Since(ThroughputCounters earlier) => new(
        Math.Max(0, Received - earlier.Received),
        Math.Max(0, Sent - earlier.Sent));
}

/// <summary>
/// Reads Windows' own byte counters for a network interface.
///
/// Everything here is local and read-only. Nothing in this file sends a
/// packet, resolves a name, or contacts anything: the figures the Connection
/// card shows are measured off the adapter, never estimated and never
/// transferred. That is the whole reason the card can claim what it claims.
/// </summary>
public static class NetworkThroughput
{
    /// <summary>
    /// The counters for the interface with this id, or null when Windows no
    /// longer reports one — an adapter that has just been disabled, an id that
    /// never existed, or a driver that will not answer for its own statistics.
    ///
    /// Null is a real answer and is passed through as one. The caller says the
    /// reading is unavailable; it does not substitute a zero.
    /// </summary>
    public static ThroughputCounters? Read(string interfaceId)
    {
        try
        {
            // Resolved afresh on every read rather than cached. BigRat
            // restarts the adapter after a MAC change, and a NetworkInterface
            // held across that restart describes an adapter that no longer
            // exists. One enumeration a second costs nothing.
            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => string.Equals(n.Id, interfaceId, StringComparison.Ordinal));

            if (networkInterface is null) return null;

            // IP statistics, not IPv4: traffic over either family crossed the
            // same adapter, and a card headed "Throughput" would be wrong to
            // count only half of it.
            var statistics = networkInterface.GetIPStatistics();
            return new ThroughputCounters(statistics.BytesReceived, statistics.BytesSent);
        }
        catch (Exception ex) when (ex is NetworkInformationException
                                      or PlatformNotSupportedException
                                      or NotSupportedException
                                      or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
