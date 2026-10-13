namespace MacChanger.Core;

public interface IAdapterRegistryLocator
{
    string? FindAdapterKey(string adapterId);
}
