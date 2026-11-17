using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacChanger.App.Services;

/// <summary>One recorded MAC operation.</summary>
public sealed record HistoryEntry(
    DateTimeOffset Timestamp,
    string AdapterName,
    string Action,
    string From,
    string To,
    bool Success,
    string Message,
    bool? Verified = null)
{
    /// <summary>
    /// When this ran, in the form the pages show it.
    ///
    /// An entry older than today carries its date too. The record now outlives
    /// the session, so a bare 14:32:10 in a list that can reach back weeks does
    /// not say which day it belonged to.
    /// </summary>
    [JsonIgnore]
    public string Time
    {
        get
        {
            var local = Timestamp.ToLocalTime();
            return local.Date == DateTimeOffset.Now.Date
                ? local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : local.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
        }
    }

    // Derived from the fields above, so they are not written to the file: the
    // arrow and the word "Succeeded" are presentation, and storing them would
    // only mean reading back text that has to be kept in step with the data.

    [JsonIgnore] public string Change => $"{From}  →  {To}";

    [JsonIgnore] public string Outcome => Success ? "Succeeded" : "Failed";

    // ------------------------------------------------- dashboard presentation
    //
    // The landing page is a glance, not the log: one short line per operation
    // instead of the sentence the MAC Changer page writes under the button.
    // The title is derived from the action BigRat actually recorded, so an
    // entry from an older version still says what it was.

    /// <summary>True only when an apply/restore succeeded *and* the adapter reported the new address.</summary>
    [JsonIgnore] public bool IsVerified => Success && Verified == true;

    /// <summary>
    /// "Address changed", "Original restored" or the matching failure. The
    /// mockup's wording, chosen from the recorded action rather than invented
    /// for the page.
    /// </summary>
    [JsonIgnore]
    public string ActivityTitle =>
        Action.Contains("Restore", StringComparison.OrdinalIgnoreCase)
            ? (Success ? "Original restored" : "Restore failed")
            : (Success ? "Address changed" : "Change failed");

    /// <summary>
    /// The quiet second line: the address that was written, plus whether the
    /// adapter confirmed it, or the reason the write did not happen.
    /// </summary>
    [JsonIgnore]
    public string ActivitySub =>
        !Success ? Message
        : Verified switch
        {
            true => $"{To} · verified",
            false => $"{To} · not verified",
            _ => To,
        };

    /// <summary>Short clock time for the dashboard row.</summary>
    [JsonIgnore] public string ActivityTime => Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    // --------------------------------------------------- history presentation

    /// <summary>"Address changed · &lt;adapter&gt;", as the timeline title.</summary>
    [JsonIgnore] public string TimelineTitle => $"{ActivityTitle} · {AdapterName}";

    /// <summary>Today's entries say "Today"; older ones carry their date.</summary>
    [JsonIgnore]
    public string TimelineTime
    {
        get
        {
            var local = Timestamp.ToLocalTime();
            return local.Date == DateTimeOffset.Now.Date
                ? $"Today {local.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                : local.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The address move, or what was attempted when the write failed.</summary>
    [JsonIgnore] public string TimelineMac => Success ? $"{From} → {To}" : $"Attempted {To}";

    /// <summary>The driver's own account of a failure; empty on success.</summary>
    [JsonIgnore] public string TimelineNote => Success ? string.Empty : Message;

    /// <summary>
    /// The chip's word. "Verified" is only claimed when verification actually
    /// happened; a write whose result was never confirmed says "Not verified"
    /// in warning colour instead of borrowing the success wording.
    /// </summary>
    [JsonIgnore]
    public string OutcomeLabel =>
        !Success ? "Rejected"
        : Verified == true ? "Verified"
        : Verified == false ? "Not verified"
        : "Succeeded";

    /// <summary>A word the status-brush converter already understands.</summary>
    [JsonIgnore]
    public string OutcomeTone =>
        !Success ? "Failed"
        : Verified == false ? "Warning"
        : "Succeeded";
}

/// <summary>
/// Records what BigRat actually did, and keeps that record in a file beside the
/// user's preferences.
///
/// Nothing is invented: an entry only appears after a real apply or restore
/// attempt, and what is stored is what the service reported. The file is
/// written after each operation rather than on the way out, so a crash or a
/// kill does not take the record of what happened to a network adapter with it.
///
/// The user has two separate controls over this, deliberately. Whether new
/// operations are written down at all (<see cref="Enabled"/>, the switch on the
/// Settings page), and whether what has already been written down is kept
/// (<see cref="Clear"/>, the button on the History page). Turning recording off
/// leaves the existing file alone: deleting a log the moment the user asks it
/// to stop growing would be a surprise, not a favour.
///
/// Every failure is non-fatal and reported rather than hidden — if the file
/// cannot be written, the entries still exist for this session and
/// <see cref="StorageFailed"/> says so, so the History page can say so too.
/// </summary>
public sealed class HistoryService
{
    /// <summary>
    /// How many operations are kept, in the file and in memory alike. A MAC
    /// changer does not need an unbounded log, and a bounded one cannot grow
    /// without limit on someone's disk.
    /// </summary>
    const int MaxEntries = 100;

    static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    readonly string filePath;

    public HistoryService(string filePath) => this.filePath = filePath;

    /// <summary>Full path of the file the record is kept in, shown on the Settings page.</summary>
    public string FilePath => filePath;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    /// <summary>
    /// Whether new operations are written down. Set from the Settings page.
    /// False stops recording and touches nothing that is already stored.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True when the last attempt to write the file failed.</summary>
    public bool StorageFailed { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Reads the stored record. Called once, at startup.</summary>
    public void Load()
    {
        Entries.Clear();
        try
        {
            if (File.Exists(filePath) &&
                JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(filePath)) is { } stored)
            {
                foreach (var entry in stored.Take(MaxEntries)) Entries.Add(entry);
            }

            StorageFailed = false;
        }
        catch
        {
            // An unreadable or malformed file means "nothing stored yet". This
            // is a log of what the application did, not information the user
            // typed: refusing to start over it would be worse than starting
            // with an empty one, and the empty state says plainly that nothing
            // has been recorded. The next operation writes the file afresh.
            Entries.Clear();
            StorageFailed = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Record(
        string adapterName,
        string action,
        string from,
        string to,
        bool success,
        string message,
        bool? verified = null)
    {
        if (!Enabled) return;

        Entries.Insert(0, new HistoryEntry(DateTimeOffset.Now, adapterName, action, from, to, success, message, verified));
        while (Entries.Count > MaxEntries) Entries.RemoveAt(Entries.Count - 1);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Empties the record and deletes the file it was kept in.
    ///
    /// Returns false when the file could not be deleted, so the caller can say
    /// that rather than report a clean sweep that did not happen.
    /// </summary>
    public bool Clear()
    {
        Entries.Clear();

        var removed = true;
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            StorageFailed = false;
        }
        catch
        {
            removed = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(filePath, JsonSerializer.Serialize(Entries.ToList(), Format));
            StorageFailed = false;
        }
        catch
        {
            StorageFailed = true;
        }
    }
}
