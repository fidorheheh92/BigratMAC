namespace MacChanger.Core;

public sealed record NetworkAdapter(
    string Id, string Name, string Status, string InterfaceType,
    string CurrentMac, string? OriginalMac, string IPv4, string IPv6,
    string Gateway, string DnsServers, long? LinkSpeed);

public sealed record OperationResult(bool Success, string Message, NetworkAdapter? Adapter = null);
