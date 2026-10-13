using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MacChanger.App.Services;

/// <summary>
/// Preferences that are read from and written to disk between runs.
///
/// The class raises change notifications so that a switch flipped on the
/// Settings page is immediately reflected anywhere else the same preference is
/// shown — there is one value, not a copy per page.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    bool restartAdapterAfterChange = true;
    bool confirmBeforeApplying = true;
    bool use02FirstOctet;
    bool keepHistory = true;
    bool sidebarCollapsed;
    string accent = AccentPalette.Default;
    string? lastAdapterId;

    /// <summary>Restart the adapter after writing the override, so the change takes effect immediately.</summary>
    public bool RestartAdapterAfterChange
    {
        get => restartAdapterAfterChange;
        set => Set(ref restartAdapterAfterChange, value);
    }

    /// <summary>Ask for confirmation before writing a MAC address.</summary>
    public bool ConfirmBeforeApplying
    {
        get => confirmBeforeApplying;
        set => Set(ref confirmBeforeApplying, value);
    }

    /// <summary>Default for the "use 02 as first octet" option on the MAC Changer page.</summary>
    public bool Use02FirstOctet
    {
        get => use02FirstOctet;
        set => Set(ref use02FirstOctet, value);
    }

    /// <summary>
    /// Whether BigRat writes down the MAC operations it performs, keeping them
    /// in a file beside these preferences so they are still there next run.
    ///
    /// Recorded rather than not, because a MAC change is the kind of thing
    /// someone needs to be able to look back at: which adapter was moved to
    /// which address, and when. On by default, and honest about what it holds —
    /// the adapter name, the addresses and the result, and nothing else.
    ///
    /// Turning it off stops anything new being written and leaves what is
    /// already stored where it is; clearing that is the separate, explicit
    /// action on the History page. The two are split because "stop recording"
    /// and "forget what you recorded" are different requests, and running them
    /// together would mean someone who only wanted the first lost the second
    /// without asking.
    /// </summary>
    public bool KeepHistory
    {
        get => keepHistory;
        set => Set(ref keepHistory, value);
    }

    /// <summary>Name of the selected accent from <see cref="AccentPalette.Options"/>.</summary>
    public string Accent
    {
        get => accent;
        set => Set(ref accent, value);
    }

    /// <summary>
    /// Whether the navigation rail is showing its collapsed, icon-only form.
    ///
    /// Toggled from the rail itself rather than from the Settings page: it is a
    /// property of the window you are looking at, not a preference you go
    /// looking for. It is stored here all the same, so the window comes back
    /// the width you left it.
    /// </summary>
    public bool SidebarCollapsed
    {
        get => sidebarCollapsed;
        set => Set(ref sidebarCollapsed, value);
    }

    /// <summary>Adapter selected when the app was last closed, so it can be re-selected on start.</summary>
    public string? LastAdapterId
    {
        get => lastAdapterId;
        set => Set(ref lastAdapterId, value);
    }

    /// <summary>
    /// Hardware addresses BigRat has watched an adapter report while it was not
    /// overridden, keyed by adapter id.
    ///
    /// This exists because Windows will not always tell us. Several drivers —
    /// Realtek's among them — answer neither Win32_NetworkAdapter.PermanentAddress
    /// nor MSFT_NetAdapter.PermanentAddress, so once a NetworkAddress override is
    /// written there is nothing left on the machine that remembers what the
    /// address used to be. This is the one place that can, and the value in it is
    /// not a guess: it is an address the adapter itself reported on an earlier
    /// run, while it was still reporting its own hardware address.
    ///
    /// Nothing binds to this, so unlike the rest of the class it raises no change
    /// notification. It is written by AppState.RefreshAdaptersAsync and saved by
    /// the same SettingsService call that saves everything else.
    /// </summary>
    public Dictionary<string, string> ObservedHardwareAddresses { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
