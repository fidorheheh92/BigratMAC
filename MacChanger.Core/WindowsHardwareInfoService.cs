using System.Globalization;
using System.Management;
using System.Runtime.Versioning;

namespace MacChanger.Core;

/// <summary>
/// Reads hardware identifiers from WMI. Everything here is strictly read-only:
/// no value is written back, cached between calls, or invented. When Windows
/// does not report a value (common for serial numbers on virtual machines) it is
/// shown as "Unavailable" rather than filled in with a plausible substitute.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareInfoService : IHardwareInfoService
{
    const string Unavailable = "Unavailable";

    public Task<IReadOnlyList<HardwareSection>> GetHardwareInfoAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<HardwareSection>>(() => new List<HardwareSection>
        {
            Query("System", "SELECT Manufacturer, Model, SystemType, NumberOfProcessors, TotalPhysicalMemory FROM Win32_ComputerSystem", row => new[]
            {
                new HardwareFact("Manufacturer", Text(row, "Manufacturer")),
                new HardwareFact("Model", Text(row, "Model")),
                new HardwareFact("System type", Text(row, "SystemType")),
                new HardwareFact("Physical processors", Number(row, "NumberOfProcessors", Count)),
                new HardwareFact("Installed memory", Number(row, "TotalPhysicalMemory", Bytes)),
            }),

            Query("System UUID", "SELECT UUID, IdentifyingNumber, Vendor, Version FROM Win32_ComputerSystemProduct", row => new[]
            {
                new HardwareFact("UUID", Text(row, "UUID")),
                new HardwareFact("Identifying number", Text(row, "IdentifyingNumber")),
                new HardwareFact("Vendor", Text(row, "Vendor")),
                new HardwareFact("Version", Text(row, "Version")),
            }),

            Query("Processor", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation FROM Win32_Processor", row => new[]
            {
                new HardwareFact("Name", Text(row, "Name")),
                new HardwareFact("Cores", Number(row, "NumberOfCores", Count)),
                new HardwareFact("Logical processors", Number(row, "NumberOfLogicalProcessors", Count)),
                new HardwareFact("Max clock", Number(row, "MaxClockSpeed", Megahertz)),
                new HardwareFact("Socket", Text(row, "SocketDesignation")),
            }),

            Query("Motherboard", "SELECT Manufacturer, Product, SerialNumber, Version FROM Win32_BaseBoard", row => new[]
            {
                new HardwareFact("Manufacturer", Text(row, "Manufacturer")),
                new HardwareFact("Product", Text(row, "Product")),
                new HardwareFact("Serial number", Text(row, "SerialNumber")),
                new HardwareFact("Version", Text(row, "Version")),
            }),

            Query("BIOS", "SELECT Manufacturer, SMBIOSBIOSVersion, Version, ReleaseDate FROM Win32_BIOS", row => new[]
            {
                new HardwareFact("Manufacturer", Text(row, "Manufacturer")),
                new HardwareFact("SMBIOS version", Text(row, "SMBIOSBIOSVersion")),
                new HardwareFact("Firmware version", Text(row, "Version")),
                new HardwareFact("Release date", Date(row, "ReleaseDate")),
            }),

            QueryMany("Memory modules", "SELECT DeviceLocator, Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory", (row, index) =>
            {
                var size = Number(row, "Capacity", Bytes);
                var speed = Number(row, "Speed", Megahertz);
                var maker = Text(row, "Manufacturer");
                var part = Text(row, "PartNumber");
                return new HardwareFact(Label(row, "DeviceLocator", $"Module {index}"), $"{size}  ·  {speed}  ·  {maker} {part}".Trim());
            }),

            QueryMany("Storage devices", "SELECT Model, Size, InterfaceType, MediaType, SerialNumber FROM Win32_DiskDrive", (row, index) =>
            {
                var size = Number(row, "Size", Bytes);
                var bus = Text(row, "InterfaceType");
                var media = Text(row, "MediaType");
                return new HardwareFact(Label(row, "Model", $"Disk {index}"), $"{size}  ·  {bus}  ·  {media}");
            }),

            QueryMany("Graphics", "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController", (row, index) =>
            {
                var driver = Text(row, "DriverVersion");
                var memory = Number(row, "AdapterRAM", Bytes);
                return new HardwareFact(Label(row, "Name", $"Adapter {index}"), $"Driver {driver}  ·  {memory}");
            }),

            Query("Operating system", "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate FROM Win32_OperatingSystem", row => new[]
            {
                new HardwareFact("Edition", Text(row, "Caption")),
                new HardwareFact("Version", Text(row, "Version")),
                new HardwareFact("Build", Text(row, "BuildNumber")),
                new HardwareFact("Architecture", Text(row, "OSArchitecture")),
                new HardwareFact("Installed", Date(row, "InstallDate")),
            }),
        }, cancellationToken);

    // ----------------------------------------------------------------- queries

    /// <summary>Runs a query that is expected to return a single row.</summary>
    static HardwareSection Query(string title, string query, Func<ManagementBaseObject, IEnumerable<HardwareFact>> project)
    {
        var facts = new List<HardwareFact>();
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();
            foreach (ManagementBaseObject row in results)
            {
                using (row) facts.AddRange(project(row));
                break;
            }
        }
        catch (Exception ex)
        {
            return Failed(title, ex);
        }

        return facts.Count == 0
            ? new HardwareSection(title, "Windows returned no data for this component.", Array.Empty<HardwareFact>())
            : new HardwareSection(title, string.Empty, facts);
    }

    /// <summary>Runs a query that may return several rows — one fact per row.</summary>
    static HardwareSection QueryMany(string title, string query, Func<ManagementBaseObject, int, HardwareFact> project)
    {
        var facts = new List<HardwareFact>();
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();
            foreach (ManagementBaseObject row in results)
            {
                using (row) facts.Add(project(row, facts.Count + 1));
            }
        }
        catch (Exception ex)
        {
            return Failed(title, ex);
        }

        return facts.Count == 0
            ? new HardwareSection(title, "Windows returned no data for this component.", Array.Empty<HardwareFact>())
            : new HardwareSection(title, string.Empty, facts);
    }

    static HardwareSection Failed(string title, Exception ex) =>
        new(title,
            $"This component could not be read ({ex.GetType().Name}). Some values require administrator rights.",
            Array.Empty<HardwareFact>());

    // ------------------------------------------------------------ value access

    static string Text(ManagementBaseObject row, string property)
    {
        try
        {
            var value = row[property]?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? Unavailable : value;
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>Reads a property, falling back to a generated label when absent.</summary>
    static string Label(ManagementBaseObject row, string property, string fallback)
    {
        var value = Text(row, property);
        return value == Unavailable ? fallback : value;
    }

    static string Number(ManagementBaseObject row, string property, Func<double, string> format)
    {
        try
        {
            var raw = row[property];
            if (raw is null) return Unavailable;
            return format(Convert.ToDouble(raw, CultureInfo.InvariantCulture));
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>WMI dates arrive as CIM_DATETIME strings, e.g. "20240115000000.000000+000".</summary>
    static string Date(ManagementBaseObject row, string property)
    {
        try
        {
            var raw = row[property]?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return Unavailable;
            return ManagementDateTimeConverter.ToDateTime(raw).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch
        {
            return Unavailable;
        }
    }

    // --------------------------------------------------------------- formatting

    static string Count(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    static string Megahertz(double value) =>
        value <= 0 ? Unavailable : $"{value.ToString("N0", CultureInfo.InvariantCulture)} MHz";

    static string Bytes(double value)
    {
        if (value <= 0) return Unavailable;
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
