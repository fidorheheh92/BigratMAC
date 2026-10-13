using System.Runtime.Versioning;
namespace MacChanger.Core;
[SupportedOSPlatform("windows")]
public sealed class WindowsMacChangeService : INetworkAdapterService
{
 readonly INetworkAdapterService discovery; readonly IWindowsCommandExecutor executor; readonly IAdapterRegistryLocator registryLocator;
 public WindowsMacChangeService(INetworkAdapterService discovery,IWindowsCommandExecutor executor):this(discovery,executor,new WindowsAdapterRegistryLocator()){}
 public WindowsMacChangeService(INetworkAdapterService discovery,IWindowsCommandExecutor executor,IAdapterRegistryLocator registryLocator){this.discovery=discovery;this.executor=executor;this.registryLocator=registryLocator;}
 public Task<IReadOnlyList<NetworkAdapter>> GetAdaptersAsync(CancellationToken ct=default)=>discovery.GetAdaptersAsync(ct);
 public async Task<OperationResult> ApplyMacAsync(string id,string mac,bool persistent,bool restart,CancellationToken ct=default){if(!MacAddress.TryParse(mac,out var normalized))return new(false,"The MAC address is invalid.");var a=(await discovery.GetAdaptersAsync(ct)).FirstOrDefault(x=>x.Id==id);if(a is null)return new(false,"The adapter is no longer available.");var key=registryLocator.FindAdapterKey(a.Id);if(key is null)return new(false,"The adapter registry key could not be located by NetCfgInstanceId.");var command=WindowsCommandPlan.SetNetworkAddress(key,normalized);if(restart)command+="; "+WindowsCommandPlan.RestartAdapter(a.Name);var r=await executor.ExecuteAsync(command,ct);return r.Success?new(true,restart?"MAC override written and adapter restarted; refresh required to verify.":"MAC override written. Restart the adapter to activate it."):r;}
 // Removing an override does not require knowing the hardware address:
 // ClearNetworkAddress deletes the NetworkAddress value by name rather than
 // replacing it with a value, so the address was never needed for the write.
 //
 // The guard this replaces — "Original MAC is unavailable for this adapter" —
 // therefore refused a perfectly possible operation, and it refused it hardest
 // on the adapters that needed it most. A driver that answers neither
 // Win32_NetworkAdapter.PermanentAddress nor MSFT_NetAdapter.PermanentAddress
 // reports nothing once an override is in place, and refusing here left Device
 // Manager as the only way back.
 //
 // OriginalMac being null is itself the evidence that an override is present:
 // with no override, discovery reports the address in use as the hardware
 // address, because that is what it is.
 public async Task<OperationResult> RestoreOriginalAsync(string id,bool persistent,bool restart,CancellationToken ct=default)
 {
  var a=(await discovery.GetAdaptersAsync(ct)).FirstOrDefault(x=>x.Id==id);
  if(a is null)return new(false,"The adapter is no longer available.");
  var key=registryLocator.FindAdapterKey(a.Id);
  if(key is null)return new(false,"The adapter registry key could not be located by NetCfgInstanceId.");
  if(a.OriginalMac is not null&&string.Equals(a.CurrentMac,a.OriginalMac,StringComparison.OrdinalIgnoreCase))
   return new(false,"This adapter is already using its own hardware address; there is no override to remove.");
  var command=WindowsCommandPlan.ClearNetworkAddress(key);
  if(restart)command+="; "+WindowsCommandPlan.RestartAdapter(a.Name);
  var r=await executor.ExecuteAsync(command,ct);
  return r.Success?new(true,restart?"MAC override removed and adapter restarted; refresh required to verify.":"MAC override removed. Restart the adapter to restore the hardware MAC."):r;
 }
}
