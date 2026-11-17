using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Appearance and behaviour preferences, grouped into short sections with a
/// save bar that stays at the foot of the page.
///
/// The accent list is live: picking a colour re-themes the application at
/// once. Everything else is a plain binding onto <see cref="AppSettings"/>.
///
/// The save bar compares the current values with what the last successful
/// write actually stored, rather than raising a dirty flag on every change.
/// That way a switch flipped and flipped back returns to "All changes saved"
/// instead of claiming work that no longer exists, and a failed write never
/// reads as saved.
/// </summary>
public partial class SettingsView : UserControl
{
    readonly AppState state;

    // What the last successful save wrote.
    bool savedRestart;
    bool savedConfirm;
    bool savedUse02;
    bool savedKeepHistory;
    string savedAccent = string.Empty;

    public SettingsView(AppState state)
    {
        InitializeComponent();
        this.state = state;

        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        RuntimeText.Text = RuntimeInformation.FrameworkDescription;

        // Snapshot before the bindings resolve, so the initial selection the
        // ListBox makes is not mistaken for a change.
        CaptureSaved();

        DataContext = state;
        state.Settings.PropertyChanged += SettingsChanged;
    }

    // -------------------------------------------------------- accent swatches

    /// <summary>
    /// Applies the picked accent immediately. It is stored in memory too, but
    /// the file is only rewritten when Save is pressed.
    /// </summary>
    void AccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: AccentOption option }) return;

        state.Settings.Accent = option.Name;
        AccentPalette.Apply(option.Name);
        UpdateSaveState();
    }

    // ------------------------------------------------------------- save bar

    void SettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Window geometry and the last adapter are written back by the shell,
        // not chosen here, so they must not make the page look edited.
        if (e.PropertyName is not (nameof(AppSettings.RestartAdapterAfterChange)
            or nameof(AppSettings.ConfirmBeforeApplying)
            or nameof(AppSettings.Use02FirstOctet)
            or nameof(AppSettings.KeepHistory)
            or nameof(AppSettings.Accent)))
            return;

        UpdateSaveState();
    }

    bool HasUnsavedChanges =>
        state.Settings.RestartAdapterAfterChange != savedRestart ||
        state.Settings.ConfirmBeforeApplying != savedConfirm ||
        state.Settings.Use02FirstOctet != savedUse02 ||
        state.Settings.KeepHistory != savedKeepHistory ||
        !string.Equals(state.Settings.Accent, savedAccent, StringComparison.OrdinalIgnoreCase);

    void CaptureSaved()
    {
        savedRestart = state.Settings.RestartAdapterAfterChange;
        savedConfirm = state.Settings.ConfirmBeforeApplying;
        savedUse02 = state.Settings.Use02FirstOctet;
        savedKeepHistory = state.Settings.KeepHistory;
        savedAccent = state.Settings.Accent;
    }

    void UpdateSaveState()
    {
        if (HasUnsavedChanges)
        {
            SetSaveCheckVisible(false);
            SaveStateText.Text = "Unsaved changes";
            SaveStateText.Foreground = (Brush)FindResource("Brush.TextSecondary");
            return;
        }

        SetSaveCheckVisible(true);
        SaveStateText.Text = "All changes saved";
        SaveStateText.Foreground = (Brush)FindResource("Brush.TextSecondary");
    }

    /// <summary>
    /// Fades the check rather than collapsing it, so the label beside it does
    /// not jump sideways as the state changes. Reduced-motion users get the
    /// same states without the 200ms fade.
    /// </summary>
    void SetSaveCheckVisible(bool visible)
    {
        SaveCheck.BeginAnimation(UIElement.OpacityProperty, null);

        if (!SystemParameters.ClientAreaAnimation)
        {
            SaveCheck.Opacity = visible ? 1 : 0;
            return;
        }

        SaveCheck.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            To = visible ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(200),
        });
    }

    /// <summary>
    /// Writes the preferences and reports what the file system did. A failed
    /// write keeps the unsaved state and names the failure; it never displays
    /// "saved" over a file that was not written.
    /// </summary>
    void SaveClick(object sender, RoutedEventArgs e)
    {
        if (state.SaveSettings())
        {
            CaptureSaved();
            UpdateSaveState();
            state.ShowToast("Preferences saved.");
            return;
        }

        SetSaveCheckVisible(false);
        SaveStateText.Text = "Could not save preferences.";
        SaveStateText.Foreground = (Brush)FindResource("Brush.Error");
        state.ShowToast("Preferences could not be saved.", true);
    }
}
