using System.Windows;
using System.Windows.Controls;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Shows the MAC operations BigRat has performed — this run's and earlier runs'.
///
/// The list is real: it is read from the file beside the user's preferences,
/// and written back after each operation. Two things the page does not hide:
/// whether recording is currently off, and whether the file could not be
/// written, both of which are said above the list rather than left to be
/// inferred from a page that quietly stopped changing.
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

    void ClearClick(object sender, RoutedEventArgs e)
    {
        var count = state.History.Entries.Count;
        if (count == 0) return;

        var confirm = MessageBox.Show(
            $"Delete all {count} recorded {(count == 1 ? "operation" : "operations")}?\n\n" +
            "This empties the list and deletes the history file from this machine. It cannot be undone.",
            "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        // Deleting the file is the part worth confirming, so whether it really
        // went is reported rather than assumed. A file that will not delete is
        // a real answer — saying "cleared" over a file still full of addresses
        // would be the one thing this page must never do.
        if (state.History.Clear())
        {
            state.SetStatus("History cleared.");
            return;
        }

        state.SetStatus("History cleared, but the file could not be deleted.");
        MessageBox.Show(
            $"The list is empty, but the history file could not be deleted:\n\n{state.History.FilePath}\n\n" +
            "It is still on disk and will be read back the next time BigRat starts.",
            "History file not deleted", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
