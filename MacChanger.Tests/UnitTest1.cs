using MacChanger.Core;
namespace MacChanger.Tests;
public class MacTests
{
 [Theory][InlineData("AA:BB:CC:DD:EE:FF")][InlineData("AA-BB-CC-DD-EE-FF")][InlineData("AABBCCDDEEFF")] public void Parses(string s){Assert.True(MacAddress.TryParse(s,out var n));Assert.Equal("AA:BB:CC:DD:EE:FF",n);}
 [Fact] public void RejectsMulticast(){Assert.False(MacAddress.TryParse("01:00:00:00:00:01",out _));}
 [Fact] public void GeneratesLocalUnicast(){var m=MacAddress.GenerateRandom(true);Assert.True(MacAddress.TryParse(m,out _));Assert.StartsWith("02:",m);}
 [Fact] public async Task MockWorkflow(){var s=new MockNetworkAdapterService();var a=(await s.GetAdaptersAsync())[0];var r=await s.ApplyMacAsync(a.Id,MacAddress.GenerateRandom(),true,true);Assert.True(r.Success);Assert.True(s.LastPersistent);Assert.True(s.LastRestart);Assert.Equal(a.OriginalMac,(await s.GetAdaptersAsync())[0].OriginalMac);r=await s.RestoreOriginalAsync(a.Id,false,false);Assert.True(r.Success);Assert.Equal(a.OriginalMac,(await s.GetAdaptersAsync())[0].CurrentMac);}
 [Fact] public async Task RejectsFailures(){var s=new MockNetworkAdapterService();Assert.False((await s.ApplyMacAsync("missing","02:11:22:33:44:55",false,false)).Success);var id=(await s.GetAdaptersAsync())[0].Id;Assert.False((await s.ApplyMacAsync(id,"01:00:00:00:00:01",false,false)).Success);}
 [Fact] public void BuildsSafeCommands(){var set=WindowsCommandPlan.SetNetworkAddress(@"HKEY_LOCAL_MACHINE\SYSTEM\Adapter'Key","02:11:22:33:44:55");Assert.Contains("New-ItemProperty",set);Assert.Contains("-PropertyType String",set);Assert.Contains("021122334455",set);Assert.Contains("Adapter''Key",set);Assert.DoesNotContain("Set-NetAdapter",set);var restart=WindowsCommandPlan.RestartAdapter("Wi'Fi");Assert.Contains("Disable-NetAdapter",restart);Assert.Contains("Enable-NetAdapter",restart);Assert.Contains("Wi''Fi",restart);}
}
