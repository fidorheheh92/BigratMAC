using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MacChanger.Core;

namespace MacChanger.App.Services;

/// <summary>
/// The state every page shares: the services they call, the adapter list they
/// display, which adapter is selected, and the message on the status bar.
///
/// Pages are handed one instance so they cannot drift out of sync — changing
/// the selected adapter on the MAC Changer page also updates the Dashboard.
/// </summary>
public sealed class AppState : INotifyPropertyChanged
{
    /// <summary>Page keys used with <see cref="Navigate"/>.</summary>
    public const string DashboardPage = "Dashboard";
    public const string MacChangerPage = "MAC Changer";
    public const string HwidPage = "HWID";
    public const string HistoryPage = "History";
    public const string SettingsPage = "Settings";

    public AppState()
    {
        Service = new WindowsMacChangeService(Discovery, new WindowsCommandExecutor());
        Settings = SettingsStore.Load();

        // The record lives in the same folder as the preferences, and its
        // switch is a preference, so it is built here rather than in a field
        // initializer — the path and the saved setting both have to be read
        // before either can be used.
        History = new HistoryService(SettingsStore.HistoryFilePath);
        History.Enabled = Settings.KeepHistory;
        History.Load();

        // The switch takes effect the moment it is flipped, like every other
        // preference on that page. Nothing is written until the next operation
        // either way, so this only has to keep the two in step.
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AppSettings.KeepHistory)) return;
            History.Enabled = Settings.KeepHistory;
            OnPropertyChanged(nameof(KeepsHistory));
        };

        Adapters.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAdapters));
            OnPropertyChanged(nameof(HasNoAdapters));
            OnPropertyChanged(nameof(ConnectedCount));
            OnPropertyChanged(nameof(CanAct));
        };

        History.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(HasNoHistory));
            OnPropertyChanged(nameof(RecentActivity));
            OnPropertyChanged(nameof(HistoryStorageFailed));
        };

        HardwareSections.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasHardwareSections));
            OnPropertyChanged(nameof(HasNoHardwareSections));
        };
    }

    /// <summary>Reads the adapters Windows currently exposes.</summary>
    public WindowsNetworkAdapterService Discovery { get; } = new();

    /// <summary>Writes and clears the MAC override, and restarts the adapter.</summary>
    public INetworkAdapterService Service { get; }

    /// <summary>Read-only hardware identification used by the HWID page.</summary>
    public IHardwareInfoService Hardware { get; } = new WindowsHardwareInfoService();

    /// <summary>
    /// What BigRat has done, read from the history file at startup and written
    /// back after each operation.
    /// </summary>
    public HistoryService History { get; }

    public SettingsService SettingsStore { get; } = new();

    /// <summary>Preferences loaded from disk at startup.</summary>
    public AppSettings Settings { get; }

    public ObservableCollection<NetworkAdapter> Adapters { get; } = new();

    /// <summary>
    /// Live throughput for the selected adapter, for the Connection card.
    ///
    /// Sampled from Windows' own per-interface byte counters a second at a
    /// time — measured locally, never estimated, and nothing is sent anywhere
    /// to obtain it. It follows the selection, so the card always describes
    /// the adapter the rest of the page is describing.
    /// </summary>
    public ConnectionMonitor Connection { get; } = new();

    public bool HasAdapters => Adapters.Count > 0;
    public bool HasNoAdapters => Adapters.Count == 0;
    public bool HasSelection => SelectedAdapter is not null;
    public bool HasNoSelection => SelectedAdapter is null;

    /// <summary>True when an adapter is selected and nothing else is running.</summary>
    public bool CanAct => HasSelection && IsNotBusy;

    public int ConnectedCount => Adapters.Count(a =>
        string.Equals(a.Status, "Connected", StringComparison.OrdinalIgnoreCase));

    public bool HasHistory => History.Entries.Count > 0;
    public bool HasNoHistory => History.Entries.Count == 0;

    /// <summary>Whether operations are being written down at all. Bound by the Settings page.</summary>
    public bool KeepsHistory => Settings.KeepHistory;

    /// <summary>True when the record could not be written to disk, so the History page can say so.</summary>
    public bool HistoryStorageFailed => History.StorageFailed;

    /// <summary>
    /// The newest few operations, for the dashboard's activity card.
    ///
    /// The dashboard only ever showed what had happened in the session, which
    /// was bounded by how long the application had been open. Now that the
    /// record survives between runs it can hold far more than belongs on a
    /// landing page, so the card takes the top of it and the History page
    /// remains the place to read the whole thing.
    /// </summary>
    public IEnumerable<HistoryEntry> RecentActivity => History.Entries.Take(RecentActivityCount);

    const int RecentActivityCount = 5;

    NetworkAdapter? selectedAdapter;

    /// <summary>The adapter every page acts on. Kept in sync by two-way binding.</summary>
    public NetworkAdapter? SelectedAdapter
    {
        get => selectedAdapter;
        set
        {
            selectedAdapter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
            OnPropertyChanged(nameof(CanAct));
            Connection.Track(value);
            if (value is not null) Settings.LastAdapterId = value.Id;
        }
    }

    // Empty by default. BigRat has no resting status line: this is only ever
    // set while something is genuinely happening, or to report a real result,
    // and it is cleared again as soon as it stops being true.
    string statusMessage = string.Empty;

    /// <summary>
    /// A transient note about what the application is doing right now. Blank
    /// unless there is something real to say — the shell collapses the strip
    /// that shows it while this is empty, so an idle BigRat has no status
    /// sentence anywhere on screen.
    /// </summary>
    public string StatusMessage
    {
        get => statusMessage;
        private set
        {
            if (statusMessage == value) return;
            statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    /// <summary>True while there is a transient message worth showing.</summary>
    public bool HasStatusMessage => statusMessage.Length > 0;

    // A counter rather than a flag: a refresh started inside a longer operation
    // must not clear the busy indicator while that operation is still running.
    int busyCount;

    /// <summary>True while an adapter scan, MAC write, or hardware read is in progress.</summary>
    public bool IsBusy => busyCount > 0;

    public bool IsNotBusy => busyCount == 0;

    public void BeginWork()
    {
        busyCount++;
        RaiseBusyChanged();
    }

    public void EndWork()
    {
        if (busyCount > 0) busyCount--;
        RaiseBusyChanged();
    }

    /// <summary>Set by the shell so pages can send the user to another page.</summary>
    public Action<string>? Navigate { get; set; }

    /// <summary>Raised when something failed and the shell should tell the user.</summary>
    public event EventHandler<string>? Error;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetStatus(string message) => StatusMessage = message;

    /// <summary>Asks the shell to show a dialog; keeps dialog code out of the state object.</summary>
    public void ReportError(string message) => Error?.Invoke(this, message);

    /// <summary>
    /// Re-reads the adapter list. The previously selected adapter is re-selected
    /// by id when it is still present, so a refresh never silently moves the user
    /// onto a different network interface.
    /// </summary>
    public async Task RefreshAdaptersAsync()
    {
        if (isScanning) return;
        isScanning = true;
        BeginWork();
        StatusMessage = "Scanning network adapters…";
        try
        {
            var previousId = SelectedAdapter?.Id ?? Settings.LastAdapterId;
            var found = await Task.Run(() => Discovery.GetAdaptersAsync());

            Adapters.Clear();
            foreach (var adapter in found) Adapters.Add(Remember(adapter));

            SelectedAdapter = Adapters.FirstOrDefault(a => a.Id == previousId) ?? Adapters.FirstOrDefault();

            // Success needs no sentence: the adapter list on screen is the
            // answer. The only outcome worth writing down is the one where
            // the user would otherwise be looking at an empty page wondering
            // why, so that case — and nothing else — gets a message.
            StatusMessage = Adapters.Count == 0
                ? "No supported network adapters were found."
                : string.Empty;
        }
        catch (Exception ex)
        {
            Adapters.Clear();
            SelectedAdapter = null;
            StatusMessage = "Adapter scan failed.";
            ReportError(ex.Message);
        }
        finally
        {
            isScanning = false;
            EndWork();
        }
    }

    /// <summary>
    /// Notes an adapter's hardware address as it comes back from a scan, and
    /// fills it back in later when the discovery service could not work it out.
    ///
    /// Discovery answers with the hardware address whenever it genuinely can:
    /// either the driver named it, or no override is in place, in which case the
    /// address in use *is* the hardware address. Both are worth writing down.
    /// On a driver that refuses to report the permanent address — which is what
    /// Realtek's do — this record is the only thing that lets the original
    /// address survive the first override.
    ///
    /// When discovery comes back empty it is saying "this adapter has been
    /// overridden and nothing on this machine remembers what preceded it", and
    /// the remembered value is used if there is one. If there is not, the
    /// adapter is passed through untouched and the pages say the address is not
    /// available, which is the true answer.
    /// </summary>
    NetworkAdapter Remember(NetworkAdapter adapter)
    {
        if (adapter.OriginalMac is not null)
        {
            Settings.ObservedHardwareAddresses[adapter.Id] = adapter.OriginalMac;
            return adapter;
        }

        // Re-parsed rather than trusted: it came off disk, and everything else
        // that reaches the interface as an address has been through TryParse.
        return Settings.ObservedHardwareAddresses.TryGetValue(adapter.Id, out var seen)
               && MacAddress.TryParse(seen, out var normalized)
            ? adapter with { OriginalMac = normalized }
            : adapter;
    }

    /// <summary>Runs an operation with the busy indicator on, always clearing it afterwards.</summary>
    public async Task RunAsync(Func<Task> operation)
    {
        BeginWork();
        try
        {
            await operation();
        }
        finally
        {
            EndWork();
        }
    }

    // ------------------------------------------------------------ hardware page

    /// <summary>Hardware identifiers read from Windows. Read-only; never written back.</summary>
    public ObservableCollection<HardwareSection> HardwareSections { get; } = new();

    public bool HasHardwareSections => HardwareSections.Count > 0;
    public bool HasNoHardwareSections => HardwareSections.Count == 0;

    string hardwareStatus = string.Empty;

    /// <summary>Explains why the hardware list is empty. Blank while it is populated.</summary>
    public string HardwareStatus
    {
        get => hardwareStatus;
        private set
        {
            if (hardwareStatus == value) return;
            hardwareStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the hardware identifiers from WMI. Never modifies anything.</summary>
    public async Task LoadHardwareInfoAsync()
    {
        if (isReadingHardware) return;
        isReadingHardware = true;
        BeginWork();
        StatusMessage = "Reading hardware information…";
        try
        {
            var sections = await Task.Run(() => Hardware.GetHardwareInfoAsync());

            HardwareSections.Clear();
            foreach (var section in sections) HardwareSections.Add(section);

            HardwareStatus = HardwareSections.Count == 0
                ? "Windows did not report any hardware information."
                : string.Empty;

            // Nothing changed, so there is nothing to announce. The sections
            // appearing on the page is the whole message.
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            HardwareSections.Clear();
            HardwareStatus = "Hardware information could not be read on this system.";
            StatusMessage = "Hardware read failed.";
            ReportError(ex.Message);
        }
        finally
        {
            isReadingHardware = false;
            EndWork();
        }
    }

    bool isScanning;
    bool isReadingHardware;

    /// <summary>Persists preferences and reports whether the file was really written.</summary>
    public bool SaveSettings() => SettingsStore.Save(Settings);

    void RaiseBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(CanAct));
    }

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
