namespace MacChanger.Core;

public sealed class MockNetworkAdapterService : INetworkAdapterService
{
    public bool LastPersistent { get; private set; }
    public bool LastRestart { get; private set; }
    private readonly Dictionary<string, NetworkAdapter> _adapters = new()
    {
        ["wifi-01"] = new("wifi-01", "Wi-Fi", "Connected", "Wireless", "3C:52:82:10:20:30", "3C:52:82:10:20:30", "192.168.1.42", "fe80::1234", "192.168.1.1", "192.168.1.1", 866_700_000),
        ["eth-01"] = new("eth-01", "Ethernet", "Disconnected", "Ethernet", "A8:BB:CC:DD:EE:01", "A8:BB:CC:DD:EE:01", "Unavailable", "Unavailable", "Unavailable", "Unavailable", 1_000_000_000)
    };

    public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NetworkAdapter>>(_adapters.Values.ToList());

    public Task<OperationResult> ApplyMacAsync(string adapterId, string mac, bool persistent, bool restart, CancellationToken cancellationToken = default)
    {
        LastPersistent = persistent;
        LastRestart = restart;
        if (!MacAddress.TryParse(mac, out var normalized)) return Task.FromResult(new OperationResult(false, "The MAC address is invalid."));
        if (!_adapters.TryGetValue(adapterId, out var adapter)) return Task.FromResult(new OperationResult(false, "The adapter is no longer available."));
        _adapters[adapterId] = adapter with { CurrentMac = normalized };
        return Task.FromResult(new OperationResult(true, "Mock change applied and verified.", _adapters[adapterId]));
    }

    public Task<OperationResult> RestoreOriginalAsync(string adapterId, bool persistent, bool restart, CancellationToken cancellationToken = default)
    {
        LastPersistent = persistent;
        LastRestart = restart;
        if (!_adapters.TryGetValue(adapterId, out var adapter)) return Task.FromResult(new OperationResult(false, "The adapter is no longer available."));
        if (adapter.OriginalMac is null || !MacAddress.TryParse(adapter.OriginalMac, out var original)) return Task.FromResult(new OperationResult(false, "Original MAC is unavailable for this adapter."));
        _adapters[adapterId] = adapter with { CurrentMac = original };
        return Task.FromResult(new OperationResult(true, "Mock original MAC restored and verified.", _adapters[adapterId]));
    }
}
