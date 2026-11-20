using System.Text.Json;
using System.Text.Json.Serialization;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class InstalledProgramChronologyTests
{
    private static readonly DateTime Day = new(2026, 11, 16);
    private static InstalledProgramInfo App(string name, double hour) => new(name, "Example", "1", "Win32", "—", "", "",
        InstalledOn: Day, Identity: name, RegistrationModifiedOn: Day.AddHours(hour));

    [Fact] public void LaterInstallBeatsAlphabeticalOrderOnSameDay()
    {
        var items = new[] { App("Git", 8), App("MAX", 9), App("Thermiqra Removal Lab", 15.5) };
        Assert.Equal(new[] { "Thermiqra Removal Lab", "MAX", "Git" }, InstalledProgramChronology.Sort(items, false).Select(a => a.Name));
    }

    [Fact] public void NameSortRemainsAlphabetical() => Assert.Equal(new[] { "Git", "MAX", "Thermiqra Removal Lab" },
        InstalledProgramChronology.Sort(new[] { App("MAX", 9), App("Thermiqra Removal Lab", 15), App("Git", 8) }, true).Select(a => a.Name));

    [Fact] public void NativeTimestampPreservedAndMissingDateGoesLast()
    {
        var earlier = App("A", 20) with { InstalledOn = Day.AddHours(10) };
        var later = App("Z", 8) with { InstalledOn = Day.AddHours(11) };
        var unknown = App("0", 23) with { InstalledOn = null };
        Assert.Equal(new[] { "Z", "A", "0" }, InstalledProgramChronology.Sort(new[] { unknown, earlier, later }, false).Select(a => a.Name));
    }

    [Fact] public void LaterRegistryRepairDoesNotInventNewInstallationDay()
    {
        var old = App("Old", 12) with { InstalledOn = Day.AddDays(-20) };
        Assert.Equal(old.InstalledOn, InstalledProgramChronology.Timestamp(old));
        Assert.Equal("New", InstalledProgramChronology.Sort(new[] { old, App("New", 8) }, false)[0].Name);
    }

    [Fact] public void MissingPrecisionNeverUsesFirstObservationAsInstallTime()
    {
        var item = App("Example", 8) with { RegistrationModifiedOn = null, FirstObservedOn = Day.AddHours(19) };
        Assert.Equal(Day, InstalledProgramChronology.Timestamp(item));
    }

    [Fact] public void CacheRoundTripKeepsIntraDayPrecision()
    {
        var item = App("Example", 15.5);
        var options = new JsonSerializerOptions { IgnoreReadOnlyProperties = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var restored = JsonSerializer.Deserialize<InstalledProgramInfo>(JsonSerializer.Serialize(item, options), options)!;
        Assert.Equal(item.RegistrationModifiedOn, restored.RegistrationModifiedOn);
        Assert.Equal(Day.AddHours(15.5), InstalledProgramChronology.Timestamp(restored));
    }

    [Fact] public void PackageDateParserKeepsHoursMinutesAndSeconds()
    {
        var timestamp = DateTime.Today.AddHours(8).AddMinutes(23).AddSeconds(17);
        var parsed = InstalledProgramsDiscoveryService.ReadStoreInstallDate(timestamp.ToString("O"));
        Assert.Equal(timestamp, parsed.Date);
    }

    [Fact] public void OldCacheWithoutNewFieldStillLoads()
    {
        var item = App("Example", 15.5);
        var options = new JsonSerializerOptions { IgnoreReadOnlyProperties = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(item, options))!.AsObject();
        json.Remove("RegistrationModifiedOn");
        var restored = json.Deserialize<InstalledProgramInfo>(options)!;
        Assert.Null(restored.RegistrationModifiedOn);
        Assert.Equal(Day, InstalledProgramChronology.Timestamp(restored));
    }
}
