using System.Windows;
using System.Windows.Threading;
using MacChanger.App.Services;

namespace MacChanger.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One shared state object, created before any window so the saved accent
        // is applied to the resource dictionary first and nothing flashes the default.
        var state = new AppState();
        AccentPalette.Apply(state.Settings.Accent);

        new MainWindow(state).Show();
    }

    static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Unexpected application error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            MessageBox.Show(ex.ToString(), "Unexpected background error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
