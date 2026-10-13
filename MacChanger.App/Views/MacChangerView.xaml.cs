using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    const string DefaultHint = "12 hexadecimal digits, for example 02:11:22:33:44:55.";

    readonly AppState state;

    public MacChangerView(AppState state)
    {
        InitializeComponent();
        this.state = state;
        DataContext = state;
    }

    async void RefreshClick(object sender, RoutedEventArgs e) => await state.RefreshAdaptersAsync();

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
            state.SetStatus("The MAC address is not valid.");
            return;
        }

        var restart = state.Settings.RestartAdapterAfterChange;
        var from = adapter.CurrentMac;

        if (state.Settings.ConfirmBeforeApplying && !Confirm(
                "Confirm MAC change",
                $"Adapter: {adapter.Name}\nCurrent address: {from}\nNew address: {target}",
                restart))
        {
            return;
        }

        ShowHint(DefaultHint, false);

        await state.RunAsync(async () =>
        {
            var result = await state.Service.ApplyMacAsync(adapter.Id, target, false, restart);
            await state.RefreshAdaptersAsync();

            if (!result.Success)
            {
                state.SetStatus(result.Message);
                state.History.Record(adapter.Name, "Change MAC address", from, target, false, result.Message);
                return;
            }

            var updated = state.Adapters.FirstOrDefault(a => a.Id == adapter.Id);
            var verified = updated is not null && updated.CurrentMac.Equals(target, StringComparison.OrdinalIgnoreCase);

            string note;
            if (verified)
            {
                note = "Windows reports the new address, so the change is active.";
                state.SetStatus($"Address changed and verified on {adapter.Name}.");
            }
            else if (!restart)
            {
                note = "Override written. The adapter needs a restart before the new address is active.";
                state.SetStatus("Override written — restart the adapter to activate it.");
            }
            else
            {
                note = "The command completed, but Windows still reports the previous address.";
                state.SetStatus("Written, but not verified: the adapter still reports the previous address.");
            }

            state.History.Record(adapter.Name, "Change MAC address", from, target, true, note);

            if (!verified && restart)
            {
                MessageBox.Show(
                    "The command completed, but Windows still reports the previous MAC address for this adapter.\n\n" +
                    "Some drivers reject addresses outside the range the hardware accepts.",
                    "Verification warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
        // be read is exactly the one with no other way back. This used to stop
        // at a hint saying there was nothing to restore, which was not true.
        var original = adapter.OriginalMac;

        var restart = state.Settings.RestartAdapterAfterChange;
        var from = adapter.CurrentMac;

        if (state.Settings.ConfirmBeforeApplying && !Confirm(
                "Confirm restore",
                original is null
                    ? $"Adapter: {adapter.Name}\n" +
                      "This removes BigRat's override and lets the adapter report its own hardware address again.\n\n" +
                      "BigRat cannot name that address in advance: this adapter's driver does not report the burned-in address. " +
                      "It will read it, and keep it, once the override is gone."
                    : $"Adapter: {adapter.Name}\nThis removes BigRat's override and lets the adapter use its original hardware address ({original}).",
                restart))
        {
            return;
        }

        ShowHint(DefaultHint, false);

        await state.RunAsync(async () =>
        {
            var result = await state.Service.RestoreOriginalAsync(adapter.Id, false, restart);
            await state.RefreshAdaptersAsync();

            if (!result.Success)
            {
                state.SetStatus(result.Message);
                state.History.Record(adapter.Name, "Restore original address", from, original ?? "Not available", false, result.Message);
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
            if (restored)
            {
                if (original is null)
                {
                    note = $"Override removed. The adapter reports {discovered} as its hardware address, and BigRat has kept it.";
                    state.SetStatus($"Override removed — {adapter.Name} reports {discovered}.");
                }
                else
                {
                    note = "Windows reports the hardware address again, so the override is gone.";
                    state.SetStatus($"Original address restored on {adapter.Name}.");
                }
            }
            else if (!restart)
            {
                note = "Override removed. The adapter needs a restart before the hardware address is active.";
                state.SetStatus("Override removed — restart the adapter to activate the hardware address.");
            }
            else
            {
                note = "The command completed, but Windows still reports an overridden address.";
                state.SetStatus("Override removed, but not verified.");
            }

            state.History.Record(adapter.Name, "Restore original address", from, discovered, true, note);

            if (!restored && restart)
            {
                MessageBox.Show(
                    "The command completed, but Windows still reports a non-hardware MAC address for this adapter.",
                    "Verification warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });
    }

    /// <summary>Shows the confirmation dialog, spelling out what is about to happen.</summary>
    static bool Confirm(string title, string details, bool restart)
    {
        var text = details;
        if (restart) text += "\n\nThe adapter will be restarted, so the connection drops for a moment.";
        text += "\n\nContinue?";
        return MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    void ShowHint(string message, bool isError)
    {
        NewMacHint.Text = message;
        NewMacHint.Foreground = (Brush)FindResource(isError ? "Brush.Error" : "Brush.TextSecondary");
    }
}
