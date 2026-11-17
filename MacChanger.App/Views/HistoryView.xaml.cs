using System.Windows;
using System.Windows.Controls;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Shows the MAC operations BigRat has performed — this run's and earlier runs'.
///
/// The list is real: it is read from the file beside the user's preferences,
/// and written back after each operation. Three things the page does not hide:
/// whether recording is currently off, whether the file could not be written,
/// and whether a clear left the file on disk. All three are said above the list
/// rather than left to be inferred from a page that quietly stopped changing.
/// </summary>
public partial class HistoryView : UserControl
{
    readonly AppState state;

    public HistoryView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;
    }

    /// <summary>The empty state's one button: send the user somewhere they can act.</summary>
    void NavigateClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page }) state.Navigate?.Invoke(page);
    }

    async void ClearClick(object sender, RoutedEventArgs e)
    {
        var count = state.History.Entries.Count;
        if (count == 0) return;

        // Always asked through the in-app dialog. If the shell somehow has not
        // supplied one, the page does not fall back to deleting silently: a
        // destructive action with no confirmation is worse than no action.
        if (state.Confirm is not { } confirm) return;

        var confirmed = await confirm(new ConfirmRequest(
            "Clear the recorded history?",
            $"Delete all {count} recorded {(count == 1 ? "operation" : "operations")}? " +
            "This empties the list and deletes the history file from this machine.",
            "Delete history",
            Note: "This cannot be undone.",
            Destructive: true));

        if (!confirmed) return;

        // Deleting the file is the part worth confirming, so whether it really
        // went is reported rather than assumed. A file that will not delete is
        // a real answer — saying "cleared" over a file still full of addresses
        // would be the one thing this page must never do.
        if (state.History.Clear())
        {
            ClearWarning.Visibility = Visibility.Collapsed;
            state.ShowToast("History cleared.");
            return;
        }

        ClearWarningText.Text =
            $"The list is empty, but the history file could not be deleted:\n{state.History.FilePath}\n" +
            "It is still on disk and will be read back the next time BigRat starts.";
        ClearWarning.Visibility = Visibility.Visible;
        state.ShowToast("History cleared, but the file could not be deleted.", true);
    }
}
