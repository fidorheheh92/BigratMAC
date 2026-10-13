namespace MacChanger.Core;

/// <summary>A single read-only label/value pair shown on the HWID page.</summary>
public sealed record HardwareFact(string Label, string Value);

/// <summary>
/// A group of related hardware facts. <see cref="Note"/> is empty on success and
/// carries a plain-language reason when the section could not be read.
/// </summary>
public sealed record HardwareSection(string Title, string Note, IReadOnlyList<HardwareFact> Facts)
{
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

/// <summary>
/// Read-only hardware identification. Implementations only query the operating
/// system; they never modify, spoof, or persist any identifier.
/// </summary>
public interface IHardwareInfoService
{
    Task<IReadOnlyList<HardwareSection>> GetHardwareInfoAsync(CancellationToken cancellationToken = default);
}
