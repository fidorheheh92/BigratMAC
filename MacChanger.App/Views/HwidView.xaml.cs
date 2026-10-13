using System.Windows;
using System.Windows.Controls;
using MacChanger.App.Services;

namespace MacChanger.App.Views;

/// <summary>
/// Read-only hardware identification. Everything shown here is queried from
/// Windows on demand; the page offers no way to alter any of it.
/// </summary>
public partial class HwidView : UserControl
{
    readonly AppState state;

    public HwidView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;
        Loaded += async (_, _) =>
        {
            // Read once on first visit; "Read again" refreshes on demand.
            if (state.HardwareSections.Count == 0) await state.LoadHardwareInfoAsync();
        };
    }

    async void ReloadClick(object sender, RoutedEventArgs e) => await state.LoadHardwareInfoAsync();
}
