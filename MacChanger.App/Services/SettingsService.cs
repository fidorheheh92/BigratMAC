using System.IO;
using System.Text.Json;

namespace MacChanger.App.Services;

/// <summary>
/// Stores user preferences as JSON under %APPDATA%\BigRat\settings.json.
/// Uses System.Text.Json, which ships with .NET — no extra package.
/// Every failure is non-fatal: the app falls back to defaults rather than
/// refusing to start, and <see cref="Save"/> reports whether it really wrote.
/// </summary>
public sealed class SettingsService
{
    static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public SettingsService()
    {
        FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BigRat");
        FilePath = Path.Combine(FolderPath, "settings.json");
        HistoryFilePath = Path.Combine(FolderPath, "history.json");
    }

    /// <summary>The one folder BigRat keeps anything in, under %APPDATA%.</summary>
    public string FolderPath { get; }

    /// <summary>Full path of the settings file, shown on the Settings page.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Full path of the operation record, in the same folder as the preferences.
    ///
    /// Beside the settings file rather than somewhere of its own because it is
    /// the same kind of thing: local state that belongs to this installation
    /// and this user, in one place they can find, back up or delete.
    /// </summary>
    public string HistoryFilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // A missing or unreadable file simply means "no saved preferences yet".
            return new AppSettings();
        }
    }

    /// <summary>Writes the settings to disk. Returns false if the write failed.</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Format));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
