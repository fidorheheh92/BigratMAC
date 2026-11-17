using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacChanger.App.Services;
using MacChanger.Core;

namespace MacChanger.App.Views;

/// <summary>
/// The MAC Changer page. The controls here call exactly the same
/// <see cref="INetworkAdapterService"/> methods the application has always used —
/// the redesign only changed how they are presented.
/// </summary>
public partial class MacChangerView : UserControl
{
    const string DefaultHint = "Any 12 hex digits. BigRat writes them as AA:BB:CC:DD:EE:FF.";

    enum ResultTone { Ok, Warn, Bad }

    readonly AppState state;

    bool skeletonPulsing;

    public MacChangerView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AppState.CanAct) or nameof(AppState.HasSelection))
                UpdateApplyState();
            else if (e.PropertyName == nameof(AppState.IsScanningAdapters))
                UpdateSkeletonPulse();
        };

        UpdateApplyState();
        UpdateSkeletonPulse();
    }

    // ---------------------------------------------------------- page state

    /// <summary>
    /// The primary button is enabled only when there is an adapter to write to,
    /// nothing else is running, and the text in the box is a real address.
    /// </summary>
    void UpdateApplyState() =>
        ApplyButton.IsEnabled = state.CanAct && MacAddress.TryParse(NewMacBox.Text, out _);

    void NewMacTextChanged(object sender, TextChangedEventArgs e) => UpdateApplyState();

    /// <summary>
    /// The mockup's 1.5s skeleton pulse. It runs only while a scan is actually
    /// happening, and holds still when Windows has reduced motion enabled.
    /// </summary>
    void UpdateSkeletonPulse()
    {
        Border[] parts =
        {
            AdapterSkeletonSquare1, AdapterSkeletonBar1, AdapterSkeletonBar2,
            AdapterSkeletonSquare2, AdapterSkeletonBar3, AdapterSkeletonBar4,
        };

        if (state.IsScanningAdapters)
        {
            if (skeletonPulsing) return;
            skeletonPulsing = true;

            if (!SystemParameters.ClientAreaAnimation)
            {
                foreach (var part in parts) part.Opacity = 0.7;
                return;
            }

            var pulse = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(1.5),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(1)));

            foreach (var part in parts) part.BeginAnimation(UIElement.OpacityProperty, pulse);
            return;
        }

        if (!skeletonPulsing) return;
        skeletonPulsing = false;

        foreach (var part in parts)
        {
            part.BeginAnimation(UIElement.OpacityProperty, null);
            part.Opacity = 1;
        }
    }

    /// <summary>Enter in the address field submits, but only when the button would be enabled.</summary>
    void NewMacKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !ApplyButton.IsEnabled) return;
        e.Handled = true;
        ApplyClick(ApplyButton, new RoutedEventArgs());
    }

    void SettingsLinkClick(object sender, RoutedEventArgs e) =>
        state.Navigate?.Invoke(AppState.SettingsPage);

    // ------------------------------------------------------------- actions

    async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await state.RefreshAdaptersAsync();

        // A scan that threw is not a scan that found nothing. The shell has
        // already shown the exception; the picker's own state explains it.
        if (state.ScanFailed) return;

        if (state.HasNoAdapters)
        {
            state.ShowToast("No supported network adapters were found.", true);
            return;
        }

        state.ShowToast(state.Adapters.Count == 1
            ? "1 adapter found."
            : $"{state.Adapters.Count} adapters found.");
    }

    void GenerateClick(object sender, RoutedEventArgs e)
    {
        NewMacBox.Text = MacAddress.GenerateRandom(state.Settings.Use02FirstOctet);
        ShowHint("Generated on this machine with a cryptographically secure random number generator.", false);
    }

    async void ApplyClick(object sender, RoutedEventArgs e)
    {
        var adapter = state.SelectedAdapter;
        if (adapter is null) return;

        if (!MacAddress.TryParse(NewMacBox.Text, out var target))
        {
            ShowHint("That is not a valid MAC address. " + DefaultHint, true);
            NewMacBox.Focus();
            NewMacBox.SelectAll();
            return;
        }

        var restart = state.Settings.RestartAdapterAfterChange;
        var from = adapter.CurrentMac;

        if (state.Settings.ConfirmBeforeApplying)
        {
            var confirmed = await AskConfirmAsync(new ConfirmRequest(
                $"Change the address on {adapter.Name}?",
                restart
                    ? "BigRat writes the new address to the driver, then restarts the adapter because Windows caches the old one. The connection drops for a few seconds."
                    : "BigRat writes the new address to the driver. The adapter is not restarted, so Windows may keep reporting the previous address until it is.",
                "Change address",
                from,
                target,
                "If the driver rejects the write, the adapter keeps the address it has now."));

            if (!confirmed) return;
        }

        ShowHint(DefaultHint, false);
        ResultStrip.Visibility = Visibility.Collapsed;

        await state.RunAsync(async () =>
        {
            var result = await state.Service.ApplyMacAsync(adapter.Id, target, false, restart);
            await state.RefreshAdaptersAsync();

            if (!result.Success)
            {
                state.History.Record(adapter.Name, "Change MAC address", from, target, false, result.Message, false);
                ShowResult(result.Message, ResultTone.Bad);
                state.ShowToast("The address change failed.", true);
                return;
            }

            var updated = state.Adapters.FirstOrDefault(a => a.Id == adapter.Id);
            var verified = updated is not null && updated.CurrentMac.Equals(target, StringComparison.OrdinalIgnoreCase);

            string note;
            ResultTone tone;
            if (verified)
            {
                note = "Windows reports the new address, so the change is active.";
                tone = ResultTone.Ok;
            }
            else if (!restart)
            {
                note = "Override written. The adapter needs a restart before the new address is active.";
                tone = ResultTone.Warn;
            }
            else
            {
                note = "The command completed, but Windows still reports the previous address.";
                tone = ResultTone.Warn;
            }

            state.History.Record(adapter.Name, "Change MAC address", from, target, true, note, verified);
            ShowResult(note, tone);

            state.ShowToast(
                verified ? $"Address changed and verified on {adapter.Name}."
                : restart ? "Written, but not verified — the adapter still reports the previous address."
                : "Override written — restart the adapter to activate it.",
                restart && !verified);
        });
    }

    async void RestoreClick(object sender, RoutedEventArgs e)
    {
        var adapter = state.SelectedAdapter;
        if (adapter is null) return;

        // The hardware address is deliberately not required to get here. The
        // override is cleared by name, not replaced with a value, so nothing
        // about the write needs to know the address first — and on the drivers
        // that report no PermanentAddress, the adapter whose address could not
        // be read is exactly the one with no other way back.
        var original = adapter.OriginalMac;

        var restart = state.Settings.RestartAdapterAfterChange;
        var from = adapter.CurrentMac;

        if (state.Settings.ConfirmBeforeApplying)
        {
            var body = original is null
                ? "This removes BigRat's override and lets the adapter report its own hardware address again. "
                  + "This driver does not report the burned-in address, so BigRat cannot name it in advance; "
                  + "it will read it, and keep it, once the override is gone."
                : "This removes BigRat's override and lets the adapter use its original hardware address again.";

            if (restart)
                body += " The adapter is restarted afterwards, so the connection drops for a moment.";
            else
                body += " The adapter is not restarted, so the hardware address may not be active until it is.";

            var confirmed = await AskConfirmAsync(new ConfirmRequest(
                $"Restore the original address on {adapter.Name}?",
                body,
                "Restore address",
                from,
                original,
                "If the driver rejects the change, the adapter keeps the address it has now."));

            if (!confirmed) return;
        }

        ShowHint(DefaultHint, false);
        ResultStrip.Visibility = Visibility.Collapsed;

        await state.RunAsync(async () =>
        {
            var result = await state.Service.RestoreOriginalAsync(adapter.Id, false, restart);
            await state.RefreshAdaptersAsync();

            if (!result.Success)
            {
                state.History.Record(adapter.Name, "Restore original address", from, original ?? "Not available", false, result.Message, false);
                ShowResult(result.Message, ResultTone.Bad);
                state.ShowToast("The restore failed.", true);
                return;
            }

            var updated = state.Adapters.FirstOrDefault(a => a.Id == adapter.Id);
            var nowMac = updated?.CurrentMac ?? from;

            // With the address known beforehand, the proof that the override is
            // gone is that the adapter reports it again. Without it, the proof
            // is that the address the adapter reports has changed at all: a
            // moment ago it was reporting the override.
            var restored = original is not null
                ? nowMac.Equals(original, StringComparison.OrdinalIgnoreCase)
                : !nowMac.Equals(from, StringComparison.OrdinalIgnoreCase);

            // What the adapter now reports. On the unknown-address path this is
            // the first time BigRat has ever seen it, and it is the whole point
            // of the restore, so it gets named.
            var discovered = original ?? updated?.OriginalMac ?? "Not available";

            string note;
            ResultTone tone;
            if (restored)
            {
                if (original is null)
                {
                    note = $"Override removed. The adapter reports {discovered} as its hardware address, and BigRat has kept it.";
                }
                else
                {
                    note = "Windows reports the hardware address again, so the override is gone.";
                }
                tone = ResultTone.Ok;
            }
            else if (!restart)
            {
                note = "Override removed. The adapter needs a restart before the hardware address is active.";
                tone = ResultTone.Warn;
            }
            else
            {
                note = "The command completed, but Windows still reports an overridden address.";
                tone = ResultTone.Warn;
            }

            state.History.Record(adapter.Name, "Restore original address", from, discovered, true, note, restored);
            ShowResult(note, tone);

            state.ShowToast(
                restored ? $"Original address restored on {adapter.Name}."
                : restart ? "Override removed, but not verified."
                : "Override removed — restart the adapter to activate the hardware address.",
                restart && !restored);
        });
    }

    // -------------------------------------------------------------- helpers

    Task<bool> AskConfirmAsync(ConfirmRequest request) =>
        state.Confirm is { } confirm ? confirm(request) : Task.FromResult(false);

    void ShowResult(string message, ResultTone tone)
    {
        ResultText.Text = message;
        ResultStrip.Style = (Style)FindResource(tone switch
        {
            ResultTone.Ok => "ResultBanner.Ok",
            ResultTone.Warn => "ResultBanner.Warn",
            _ => "ResultBanner.Bad",
        });

        // The mockup colours the whole banner, text included: green for a
        // verified write, red for a rejected one. Without this the copy would
        // stay the ordinary body colour on a tinted surface.
        ResultText.Foreground = (Brush)FindResource(tone switch
        {
            ResultTone.Ok => "Brush.Positive",
            ResultTone.Warn => "Brush.Warning",
            _ => "Brush.Error",
        });

        ResultGlyph.Data = (Geometry)FindResource(tone switch
        {
            ResultTone.Ok => "Icon.Check",
            ResultTone.Warn => "Icon.Info",
            _ => "Icon.Warning",
        });

        ResultGlyph.Stroke = (Brush)FindResource(tone switch
        {
            ResultTone.Ok => "Brush.Positive",
            ResultTone.Warn => "Brush.Warning",
            _ => "Brush.Error",
        });

        ResultStrip.Visibility = Visibility.Visible;
    }

    void ShowHint(string message, bool isError)
    {
        NewMacHint.Foreground = (Brush)FindResource(isError ? "Brush.Error" : "Brush.TextSecondary");
        NewMacHint.Inlines.Clear();

        // The resting hint sets the example address in the mono face, as the
        // mockup does. Any other message is one sentence and plain.
        if (!isError && message == DefaultHint)
        {
            NewMacHint.Inlines.Add(new Run("Any 12 hex digits. BigRat writes them as "));
            NewMacHint.Inlines.Add(new Run("AA:BB:CC:DD:EE:FF")
            {
                FontFamily = (FontFamily)FindResource("Font.Mono"),
            });
            NewMacHint.Inlines.Add(new Run("."));
            return;
        }

        NewMacHint.Inlines.Add(new Run(message));
    }
}
