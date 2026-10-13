using System.Net; using System.Net.NetworkInformation; using System.Net.Sockets; using System.Management; using Microsoft.Win32; using System.Runtime.Versioning;
namespace MacChanger.Core;
[SupportedOSPlatform("windows")]
public sealed class WindowsNetworkAdapterService:INetworkAdapterService {
 public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<NetworkAdapter>>(NetworkInterface.GetAllNetworkInterfaces().Where(IsUserFacing).Select(n=>Map(n,ct)).ToList());
 public Task<OperationResult> ApplyMacAsync(string id,string mac,bool p,bool r,CancellationToken ct=default)=>Task.FromResult(new OperationResult(false,"Windows MAC changes are disabled in the read-only discovery service."));
 public Task<OperationResult> RestoreOriginalAsync(string id,bool p,bool r,CancellationToken ct=default)=>Task.FromResult(new OperationResult(false,"Original MAC restoration is disabled in the read-only discovery service."));
 static bool IsUserFacing(NetworkInterface n){if(n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel||n.OperationalStatus==OperationalStatus.NotPresent)return false;var t=$"{n.Name} {n.Description}";string[] x={"WFP","QoS","Filter Driver","LightWeight Filter","Packet Scheduler","debugger"};if(x.Any(v=>t.Contains(v,StringComparison.OrdinalIgnoreCase)))return false;var b=n.GetPhysicalAddress().GetAddressBytes();return b.Length==6&&b.Any(v=>v!=0);}
 static NetworkAdapter Map(NetworkInterface n,CancellationToken ct){ct.ThrowIfCancellationRequested();var p=n.GetIPProperties();var ips=p.UnicastAddresses.Select(x=>x.Address);string F(AddressFamily f)=>ips.FirstOrDefault(x=>x.AddressFamily==f)?.ToString()??"Unavailable";var gw=p.GatewayAddresses.Select(x=>x.Address).FirstOrDefault()?.ToString()??"Unavailable";var b=n.GetPhysicalAddress().GetAddressBytes();var m=string.Join(":",b.Select(v=>v.ToString("X2")));return new(n.Id,n.Name,n.OperationalStatus.ToString(),n.NetworkInterfaceType.ToString(),m,Original(n,m),F(AddressFamily.InterNetwork),F(AddressFamily.InterNetworkV6),gw,string.Join(", ",p.DnsAddresses),n.Speed>0?n.Speed:null);}
 // ------------------------------------------------------- the hardware address
 //
 // Windows can be asked for an adapter's burned-in (permanent) address in
 // several places, and they are filled in by different parts of the stack, so a
 // driver that declines to answer one of them frequently answers none. Realtek's
 // drivers are the case people hit: Get-NetAdapter shows an empty
 // PermanentAddress, and so does Win32_NetworkAdapter. That is why this walks a
 // list of sources instead of asking once and giving up.
 //
 // Two things here are deliberately not guesses:
 //
 //   * When no override is in place, the address the adapter is reporting *is*
 //     the hardware address. That is a fact about how NDIS works, not an
 //     inference, so it is used as a source in its own right.
 //   * When an override is in place and no source will name what preceded it,
 //     the honest answer is "not available". The value comes back null and the
 //     pages say so. It is never filled in with the override itself, which
 //     would be a fabricated identifier wearing the label of a real one.
 //
 // The application keeps its own record of addresses it has watched an adapter
 // report, which covers the case this method cannot — see
 // AppState.RefreshAdaptersAsync — but that record lives above this class, since
 // this class is stateless discovery and has nothing to persist to.

 static string? Original(NetworkInterface n, string current)
 {
     using var key = OpenAdapterKey(n.Id);

     // The registry goes first because it is local and instant, and because it
     // is where the vendor's INF writes the address the card was burned with.
     if (FromDriverKey(key) is { } burnedIn) return burnedIn;

     // Nothing in the registry. With no override in place the address in use is
     // the hardware address — a fact about how NDIS reports an adapter, not an
     // inference — so there is a real answer here and no reason to go asking
     // Windows for one. That matters: this runs for every adapter on every
     // scan, and the queries below are the expensive part.
     if (!HasOverride(key)) return MacAddress.TryParse(current, out var own) ? own : null;

     // Overridden, and the registry is silent. The two WMI classes are the only
     // remaining place the burned-in address could be. On a driver that answers
     // neither of them this is where the trail goes cold, and the caller gets
     // null — which is the truth, not a gap to be filled in with the override.
     return FromWmi(n.Id, n.Name);
 }

 /// <summary>
 /// The adapter's driver key under the network class, or null when it cannot be
 /// found. Opened once per adapter because the two questions asked of it — is
 /// there an override, and did the vendor record a permanent address — are both
 /// answered from the same key.
 /// </summary>
 static RegistryKey? OpenAdapterKey(string id)
 {
     try
     {
         var path = new WindowsAdapterRegistryLocator().FindAdapterKey(id);
         if (path is null) return null;
         using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
         // FindAdapterKey returns a full HKEY_LOCAL_MACHINE\... path; OpenSubKey
         // wants the part below the hive.
         return hklm.OpenSubKey(path[(path.IndexOf('\\') + 1)..], writable: false);
     }
     catch
     {
         return null;
     }
 }

 /// <summary>
 /// True when the adapter has a NetworkAddress override written to it — which is
 /// exactly the thing that hides the hardware address, and therefore the thing
 /// that decides whether the address in use can stand in for it.
 /// </summary>
 static bool HasOverride(RegistryKey? key)
 {
     try
     {
         return key?.GetValue("NetworkAddress") is string value && MacAddress.TryParse(value, out _);
     }
     catch
     {
         return false;
     }
 }

 /// <summary>
 /// The burned-in address as the driver itself reports it, from whichever
 /// provider will answer.
 /// </summary>
 static string? FromWmi(string id, string name)
 {
     // Win32_NetworkAdapter, joined on GUID. GUID is the adapter's
     // NetCfgInstanceId, which is the same value NetworkInterface.Id holds.
     // The connection name is tried after it only because NetConnectionID is
     // the name the user sees: it is localised, renamable, and not unique on a
     // machine with two adapters of the same model.
     if (PermanentAddress("root\\cimv2",
             $"SELECT PermanentAddress FROM Win32_NetworkAdapter WHERE GUID='{Escape(id)}'") is { } byGuid)
         return byGuid;

     if (PermanentAddress("root\\cimv2",
             $"SELECT PermanentAddress FROM Win32_NetworkAdapter WHERE NetConnectionID='{Escape(name)}'") is { } byName)
         return byName;

     // MSFT_NetAdapter is a different class in a different namespace, fed from
     // the NDIS stack rather than from the adapter's own WMI provider, and it is
     // what Get-NetAdapter reads. It is populated independently of the two
     // queries above, so a driver that answers one of them often answers the
     // other — it is worth asking even when the first two came back empty.
     return PermanentAddress("root\\StandardCimv2",
         $"SELECT PermanentAddress FROM MSFT_NetAdapter WHERE InterfaceGuid='{Escape(WithBraces(id))}'");
 }

 /// <summary>
 /// Runs one WMI query and returns the first address it can actually use. Every
 /// failure means the same thing here — this provider has no answer — so the
 /// class being absent on this Windows build, the property not existing on this
 /// build, and the provider refusing outright are all handled identically.
 /// </summary>
 static string? PermanentAddress(string scope, string query)
 {
     try
     {
         using var searcher = new ManagementObjectSearcher(scope, query);
         using var rows = searcher.Get();
         foreach (ManagementObject row in rows)
         {
             using (row)
             {
                 if (Usable(row["PermanentAddress"] as string, out var normalized)) return normalized;
             }
         }
     }
     catch
     {
     }

     return null;
 }

 /// <summary>
 /// Some vendors write the burned-in address into the driver key even when their
 /// WMI provider will not report it. It is a separate value from NetworkAddress,
 /// which holds the override.
 /// </summary>
 static string? FromDriverKey(RegistryKey? key)
 {
     try
     {
         return key?.GetValue("PermanentAddress") is string value && Usable(value, out var normalized)
             ? normalized
             : null;
     }
     catch
     {
         return null;
     }
 }

 /// <summary>
 /// A driver that answers the permanent-address query with all zeroes is saying
 /// it does not know, in the least useful way available. MacAddress.TryParse
 /// accepts that as a well-formed address, so it is rejected here rather than
 /// allowed to masquerade as the hardware address.
 /// </summary>
 static bool Usable(string? candidate, out string normalized) =>
     MacAddress.TryParse(candidate, out normalized) && normalized != "00:00:00:00:00:00";

 static string Escape(string value) => value.Replace("'", "''");

 /// <summary>NetworkInterface.Id already carries its braces; MSFT_NetAdapter's InterfaceGuid wants them.</summary>
 static string WithBraces(string id) => id.StartsWith('{') ? id : "{" + id + "}";
}
