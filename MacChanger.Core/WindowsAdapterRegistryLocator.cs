using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MacChanger.Core;

[SupportedOSPlatform("windows")]
public sealed class WindowsAdapterRegistryLocator : IAdapterRegistryLocator
{
    public const string NetworkClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

    public string? FindAdapterKey(string adapterId)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var networkClass = hklm.OpenSubKey(NetworkClassPath, writable: false);
        if (networkClass is null)
            return null;

        foreach (var subKeyName in networkClass.GetSubKeyNames())
        {
            using var adapterKey = networkClass.OpenSubKey(subKeyName, writable: false);
            var instanceId = adapterKey?.GetValue("NetCfgInstanceId") as string;
            if (string.Equals(instanceId, adapterId, StringComparison.OrdinalIgnoreCase))
                return $@"HKEY_LOCAL_MACHINE\{NetworkClassPath}\{subKeyName}";
        }

        return null;
    }
}
