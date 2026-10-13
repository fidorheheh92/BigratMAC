namespace MacChanger.Core;
public static class WindowsCommandPlan
{
 public static string SetNetworkAddress(string registryKey,string mac)
 {
  var cleanMac=mac.Replace(":",string.Empty).Replace("-",string.Empty).Trim().ToUpperInvariant();
  if(cleanMac.Length!=12||cleanMac.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException("MAC address must contain 12 hexadecimal characters.",nameof(mac));
  return $"New-ItemProperty -LiteralPath 'Registry::{Escape(registryKey)}' -Name 'NetworkAddress' -Value '{cleanMac}' -PropertyType String -Force -ErrorAction Stop";
 }
 public static string ClearNetworkAddress(string registryKey)=>$"Remove-ItemProperty -LiteralPath 'Registry::{Escape(registryKey)}' -Name 'NetworkAddress' -Force -ErrorAction SilentlyContinue";
 public static string RestartAdapter(string adapterName)=>$"Disable-NetAdapter -Name '{Escape(adapterName)}' -Confirm:$false -ErrorAction Stop; Enable-NetAdapter -Name '{Escape(adapterName)}' -Confirm:$false -ErrorAction Stop";
 static string Escape(string value)=>value.Replace("'","''");
}
