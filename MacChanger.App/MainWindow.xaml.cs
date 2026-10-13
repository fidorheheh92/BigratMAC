using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using MacChanger.App.Services;
using MacChanger.App.Views;

namespace MacChanger.App;

/// <summary>
/// The application shell: a navigation rail that switches between the five
/// pages, a title strip that carries the window controls, and the custom chrome
/// behaviour behind them.
///
/// Pages are created once and cached, so switching away and back does not lose
/// what was typed.
/// </summary>
public partial class MainWindow : Window
{
    readonly AppState state;
    readonly Dictionary<string, UserControl> pages = new();
    string currentPage = string.Empty;

    public MainWindow(AppState state)
    {
        InitializeComponent();

        this.state = state;
        DataContext = state;

        // Applied before the window is ever shown, so a rail that was left
        // collapsed last run is already narrow on the first frame rather than
        // animating shut in front of the user at startup.
        Rail.Width = state.Settings.SidebarCollapsed ? RailWidthCollapsed : RailWidth;

        state.Navigate = NavigateTo;
        state.Error += (_, message) =>
            MessageBox.Show(this, message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);

        NavigateTo(AppState.DashboardPage);

        Loaded += async (_, _) =>
        {
            // Started here rather than by the Dashboard page. The Connection
            // card labels its sparkline in seconds, and a window that only
            // advanced while that page happened to be on screen would make the
            // label untrue — so the sampling runs for as long as the window
            // does, on whichever adapter is selected.
            state.Connection.Start();
            await state.RefreshAdaptersAsync();
        };

        Closing += (_, _) =>
        {
            state.Connection.Stop();
            state.SaveSettings();
        };
    }

    // ------------------------------------------------------------ navigation

    // The rail's two widths. Numbers rather than XAML resources because the
    // collapse animation needs them as doubles on this side.

    const double RailWidth = 190;
    const double RailWidthCollapsed = 64;

    /// <summary>
    /// Narrows the rail to an icon-only strip, or opens it again.
    ///
    /// The width is animated from here rather than from a trigger on the
    /// collapsed state. Enter/ExitActions would express it, but each pass adds
    /// another storyboard to the property and RemoveStoryboard drops the value
    /// straight back to its setter before the reverse animation has run, so
    /// the second toggle snaps instead of easing. BeginAnimation replaces
    /// whatever is already animating the property with the new animation,
    /// which is exactly what a toggle needs.
    ///
    /// From is set explicitly rather than left to the animation to infer. The
    /// rail is only ever at one of these two widths — the constructor puts it
    /// at one before the window is shown, and this method is the only thing
    /// that moves it — so the width it is leaving is always known, and the
    /// animation never has to guess at a starting point.
    ///
    /// The nav rows, the brand and the adapter row do not animate: they swap
    /// their layout on the state change itself, so they have already reflowed
    /// by the time the rail has moved.
    /// </summary>
    void RailToggleClick(object sender, RoutedEventArgs e)
    {
        var collapsed = !state.Settings.SidebarCollapsed;
        state.Settings.SidebarCollapsed = collapsed;

        Rail.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            From = collapsed ? RailWidth : RailWidthCollapsed,
            To = collapsed ? RailWidthCollapsed : RailWidth,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    void NavChecked(object sender, RoutedEventArgs e)
    {
        // RadioButton, not FrameworkElement: CommandParameter is declared on
        // ButtonBase, and a property pattern resolves its member against the
        // narrowed type, so the wider type would not compile.
        if (sender is RadioButton { CommandParameter: string page }) NavigateTo(page);
    }

    /// <summary>The rail's adapter row doubles as a shortcut to the MAC Changer page.</summary>
    void AdapterRowClick(object sender, RoutedEventArgs e) => NavigateTo(AppState.MacChangerPage);

    /// <summary>Shows a page, creating it the first time it is needed.</summary>
    void NavigateTo(string page)
    {
        // Guards against the rail's own Checked event re-entering this method
        // when the checks are synchronised at the end.
        if (string.Equals(page, currentPage, StringComparison.Ordinal)) return;

        if (!pages.TryGetValue(page, out var view))
        {
            view = CreatePage(page);
            if (view is null) return;
            pages[page] = view;
        }

        currentPage = page;
        PageHost.Content = view;

        foreach (var item in NavList.Children.OfType<RadioButton>())
        {
            var isCurrent = string.Equals(item.CommandParameter as string, page, StringComparison.Ordinal);
            if (item.IsChecked != isCurrent) item.IsChecked = isCurrent;
        }
    }

    UserControl? CreatePage(string page) => page switch
    {
        AppState.DashboardPage => new DashboardView(state),
        AppState.MacChangerPage => new MacChangerView(state),
        AppState.HwidPage => new HwidView(state),
        AppState.HistoryPage => new HistoryView(state),
        AppState.SettingsPage => new SettingsView(state),
        _ => null,
    };

    // -------------------------------------------------------- window chrome
    //
    // The window rounds its own frame, and the three dots in the title strip
    // drive it.

    // Windows 11 rounds the corners of an ordinary window, but a
    // WindowStyle="None" window is not treated as one, so the shell has to be
    // asked directly. This is the documented way to ask, and it changes nothing
    // else: the window keeps its native resize borders, Aero snap and maximise
    // behaviour, which is exactly why AllowsTransparency stays off over in
    // MainWindow.xaml. Windows 10 and earlier do not implement the attribute;
    // they return an error, which is ignored, and simply stay square.

    const int DwmWindowCornerPreference = 33;
    const int DwmDoNotRound = 1;
    const int DwmRound = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWindowCorners();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        ApplyWindowCorners();
    }

    void ApplyWindowCorners()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // A rounded window that fills the screen reads as a rendering fault,
            // so maximised goes back to square.
            var preference = WindowState == WindowState.Maximized ? DwmDoNotRound : DwmRound;
            // The HRESULT is deliberately discarded: the only failures are "this
            // shell is too old to know the attribute" and "no compositor", and
            // both mean the same thing to us — carry on with square corners.
            _ = DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Not Windows. Nothing to round, and nothing to report.
        }
        catch (EntryPointNotFoundException)
        {
            // dwmapi.dll predates the attribute.
        }
    }

    // The three dots in the title strip. These go through SystemCommands rather
    // than setting WindowState by hand, because SystemCommands is what knows
    // about a window that is currently snapped or maximised: it restores to the
    // right size and raises the normal Closing event on the way out.
    // Resizing, dragging, double-click-to-maximise and Aero snap are all still
    // Windows' own, courtesy of the WindowChrome block in MainWindow.xaml.

    void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }

    void CloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}
