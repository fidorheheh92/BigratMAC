using System.Windows;
using System.Windows.Controls;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Landing page: a read-only summary of the adapters Windows reports, plus the
/// most recent operations, which now come from the history file rather than
/// from this run alone.
/// </summary>
public partial class DashboardView : UserControl
{
    readonly AppState state;

    public DashboardView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;
    }

    async void RefreshClick(object sender, RoutedEventArgs e) => await state.RefreshAdaptersAsync();

    void NavigateClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string page }) state.Navigate?.Invoke(page);
    }
}
