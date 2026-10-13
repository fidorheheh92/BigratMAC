using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Appearance and behaviour preferences. The accent list is live, and the Save
/// button reports what the file system actually did rather than assuming success.
/// </summary>
public partial class SettingsView : UserControl
{
    readonly AppState state;

    /// <summary>The accent currently painted, so simply opening the page is not
    /// mistaken for the user choosing a colour.</summary>
    string applied;

    public SettingsView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        applied = state.Settings.Accent;
        DataContext = state;

        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;
    }

    void AccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: AccentOption option }) return;
        if (string.Equals(option.Name, applied, StringComparison.OrdinalIgnoreCase)) return;

        applied = option.Name;
        state.Settings.Accent = option.Name;
        AccentPalette.Apply(option.Name);
        state.SetStatus($"{option.Name} accent applied. Press Save to keep it for next time.");
    }

    void SaveClick(object sender, RoutedEventArgs e)
    {
        var saved = state.SaveSettings();

        SaveStatus.Text = saved
            ? "Saved."
            : "Could not write the settings file.";

        SaveStatus.Foreground = (Brush)FindResource(saved ? "Brush.Positive" : "Brush.Error");
        state.SetStatus(saved ? "Preferences saved." : "Preferences could not be saved.");
    }
}
