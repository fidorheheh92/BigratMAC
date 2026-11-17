using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    readonly DispatcherTimer toastTimer;
    string currentPage = string.Empty;

    // The dialog is shown over the window rather than opened as a second one.
    // One request at a time: a page that asks twice while an answer is still
    // pending gets "no" rather than stacking two dialogs on top of each other.
    TaskCompletionSource<bool>? confirmCompletion;

    public MainWindow(AppState state)
    {
        InitializeComponent();

        this.state = state;
        DataContext = state;

        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.4) };
        toastTimer.Tick += (_, _) => HideToast();

        state.Confirm = ShowConfirmAsync;
        state.ToastRequested += ShowToast;

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
            toastTimer.Stop();
            FinishConfirm(false);
            state.Connection.Stop();
            state.SaveSettings();
        };
    }

    // ------------------------------------------------- confirmation dialog

    /// <summary>
    /// Shows the in-app confirmation and waits for the answer. Wired to
    /// <see cref="AppState.Confirm"/> so pages never touch the dialog's XAML.
    /// </summary>
    async Task<bool> ShowConfirmAsync(ConfirmRequest request)
    {
        if (confirmCompletion is not null) return false;

        DialogTitle.Text = request.Title;
        DialogBody.Text = request.Body;
        DialogConfirm.Content = request.ConfirmText;
        DialogConfirm.Style = (Style)FindResource(
            request.Destructive ? "Button.DangerSolid" : "Button.Primary");

        // The address strip appears only when both ends of the move are known.
        // A restore whose hardware address the driver will not report has no
        // "to" address, and an empty right-hand cell would read as a blank.
        var showsAddresses =
            !string.IsNullOrWhiteSpace(request.FromAddress) &&
            !string.IsNullOrWhiteSpace(request.ToAddress);
        DialogMac.Visibility = showsAddresses ? Visibility.Visible : Visibility.Collapsed;
        DialogFrom.Text = request.FromAddress ?? string.Empty;
        DialogTo.Text = request.ToAddress ?? string.Empty;

        var hasNote = !string.IsNullOrWhiteSpace(request.Note);
        DialogNotePanel.Visibility = hasNote ? Visibility.Visible : Visibility.Collapsed;
        DialogNote.Text = request.Note ?? string.Empty;

        DialogLayer.Visibility = Visibility.Visible;
        AnimateDialogIn();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        confirmCompletion = completion;

        // Moving focus in makes Escape, Tab and Space behave, and gives a
        // keyboard user a way out that is the safe answer rather than the
        // destructive one.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => DialogCancel.Focus());

        return await completion.Task;
    }

    /// <summary>Closes the dialog and answers the waiting page. Safe to call when none is open.</summary>
    void FinishConfirm(bool result)
    {
        var completion = confirmCompletion;
        if (completion is null) return;

        confirmCompletion = null;
        DialogLayer.Visibility = Visibility.Collapsed;
        completion.TrySetResult(result);
    }

    void DialogConfirmClick(object sender, RoutedEventArgs e) => FinishConfirm(true);

    void DialogCancelClick(object sender, RoutedEventArgs e) => FinishConfirm(false);

    void DialogScrimClick(object sender, MouseButtonEventArgs e) => FinishConfirm(false);

    void DialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        FinishConfirm(false);
    }

    /// <summary>
    /// The mockup's 160ms entrance: a short lift and a barely-there scale, so
    /// the card arrives rather than blinking on.
    ///
    /// Skipped when Windows animations are turned off; the dialog still opens,
    /// it just opens in place. The values are reset first, because the
    /// animation from the previous opening is still holding the properties at
    /// their end state.
    /// </summary>
    void AnimateDialogIn()
    {
        DialogCard.BeginAnimation(UIElement.OpacityProperty, null);
        DialogShift.BeginAnimation(TranslateTransform.YProperty, null);
        DialogScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        DialogScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        DialogCard.Opacity = 1;
        DialogShift.Y = 0;
        DialogScale.ScaleX = 1;
        DialogScale.ScaleY = 1;

        if (!SystemParameters.ClientAreaAnimation) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(160);

        DialogCard.Opacity = 0;
        DialogCard.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

        DialogShift.Y = 6;
        DialogShift.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(6, 0, duration) { EasingFunction = ease });

        DialogScale.ScaleX = 0.99;
        DialogScale.ScaleY = 0.99;
        DialogScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.99, 1, duration) { EasingFunction = ease });
        DialogScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.99, 1, duration) { EasingFunction = ease });
    }

    // -------------------------------------------------------------- toasts

    /// <summary>
    /// Shows a short outcome at the foot of the window. Wired to
    /// <see cref="AppState.ToastRequested"/>.
    /// </summary>
    void ShowToast(string message, bool isError)
    {
        ToastText.Text = message;
        ToastGlyph.Data = (Geometry)FindResource(isError ? "Icon.Warning" : "Icon.Check");
        ToastGlyph.Stroke = (Brush)FindResource(isError ? "Brush.Error" : "Brush.Positive");

        toastTimer.Stop();

        ToastHost.BeginAnimation(UIElement.OpacityProperty, null);
        ToastShift.BeginAnimation(TranslateTransform.YProperty, null);

        if (!SystemParameters.ClientAreaAnimation)
        {
            ToastHost.Opacity = 1;
            ToastShift.Y = 0;
            toastTimer.Start();
            return;
        }

        ToastHost.Opacity = 0;
        ToastHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        });

        ToastShift.Y = 8;
        ToastShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        toastTimer.Start();
    }

    void HideToast()
    {
        toastTimer.Stop();

        if (!SystemParameters.ClientAreaAnimation)
        {
            ToastHost.BeginAnimation(UIElement.OpacityProperty, null);
            ToastHost.Opacity = 0;
            return;
        }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) =>
        {
            ToastHost.BeginAnimation(UIElement.OpacityProperty, null);
            ToastHost.Opacity = 0;
        };
        ToastHost.BeginAnimation(UIElement.OpacityProperty, fade);
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
    /// The nav rows and the collapse row do not animate with the rail: they
    /// swap their layout on the state change itself, so they have already
    /// reflowed by the time the rail has moved. The brand lives above the
    /// navigation and does not move at all.
    /// </summary>
    void RailToggleClick(object sender, RoutedEventArgs e)
    {
        var collapsed = !state.Settings.SidebarCollapsed;
        state.Settings.SidebarCollapsed = collapsed;

        if (!SystemParameters.ClientAreaAnimation)
        {
            Rail.BeginAnimation(WidthProperty, null);
            Rail.Width = collapsed ? RailWidthCollapsed : RailWidth;
            return;
        }

        Rail.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            From = collapsed ? RailWidth : RailWidthCollapsed,
            To = collapsed ? RailWidthCollapsed : RailWidth,
            Duration = TimeSpan.FromMilliseconds(160),
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
