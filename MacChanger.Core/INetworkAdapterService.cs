namespace MacChanger.Core;

public interface INetworkAdapterService
{
    Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ApplyMacAsync(string adapterId, string mac, bool persistent, bool restart, CancellationToken cancellationToken = default);
    Task<OperationResult> RestoreOriginalAsync(string adapterId, bool persistent, bool restart, CancellationToken cancellationToken = default);
}
