using System;
using System.Collections.Generic;

namespace PCHardwareMonitor;

// ATA SMART attributes share a table format, but their meanings and RAW
// encodings are frequently drive/vendor-specific. This layer always preserves
// the original fields and applies a named family rule only when it matches.
// It never treats an absent attribute as zero.
public static class StorageAtaSmartInterpreter
{
    private sealed record DriveFamilyRule(
        string Family,
        Func<string, bool> MatchesModel,
        byte RemainingLifeAttribute,
        Func<StorageAtaSmartAttribute, byte?> ReadLifeRemaining);

    // Add supported drive FAMILIES here, not individual serial numbers.
    // A rule is deliberately conservative until checked against the maker's
    // documentation and actual SMART output from that family.
    private static readonly DriveFamilyRule[] FamilyRules =
    {
        new(
            "Kingston A400 (SA400S37)",
            model => model.StartsWith("KINGSTON SA400S37",
                StringComparison.OrdinalIgnoreCase),
            0xE7,
            attribute =>
            {
                // Kingston's SMART E7 SSD Life Left: RAW bytes [1:0].
                // Do not use the generic normalized "Current" column.
                ulong value = attribute.RawValue & 0xFFFF;
                return value <= 100 ? (byte)value : null;
            })
    };

    // ATA SMART READ ATTRIBUTE THRESHOLDS uses the same 30-record layout
    // as SMART READ DATA. Each entry stores the ID and the manufacturer's
    // normalized failure threshold. Vendor RAW values are not comparable
    // to these thresholds; zero means no usable threshold.
    public static bool TryApplyThresholds(
        StorageAtaSmartDetails smart, byte[] sector)
    {
        if (sector == null || sector.Length < 362 ||
            smart.Attributes.Count == 0)
            return false;

        Dictionary<byte, byte> thresholds = new();
        for (int i = 0; i < 30; i++)
        {
            int offset = 2 + i * 12;
            byte id = sector[offset];
            if (id == 0 || id == 0xFF)
                continue;

            // Duplicate IDs invalidate this threshold table.
            if (!thresholds.TryAdd(id, sector[offset + 1]))
                return false;
        }

        int matched = 0;
        List<StorageAtaSmartAttribute> updated = new();
        foreach (StorageAtaSmartAttribute attribute in smart.Attributes)
        {
            if (thresholds.TryGetValue(attribute.Id, out byte value) &&
                value > 0)
            {
                updated.Add(attribute with { Threshold = value });
                matched++;
            }
            else
            {
                updated.Add(attribute);
            }
        }

        if (matched == 0)
            return false;

        smart.Attributes = updated;
        return true;
    }

    public static StorageAtaSmartDetails? Parse(
        byte[] sector, string? model, ushort? mediaType)
    {
        // Byte 0..1 = SMART revision. Up to 30 twelve-byte attribute records
        // start at byte 2, each holding a 6-byte vendor-defined RAW field.
        if (sector.Length < 362)
            return null;

        List<StorageAtaSmartAttribute> attributes = new();
        HashSet<byte> seenIds = new();
        for (int i = 0; i < 30; i++)
        {
            int offset = 2 + i * 12;
            byte id = sector[offset];
            if (id == 0 || id == 0xFF)
                continue;

            // Duplicate IDs make the whole record set ambiguous.
            if (!seenIds.Add(id))
                return null;

            ulong rawValue = 0;
            for (int j = 0; j < 6; j++)
                rawValue |= (ulong)sector[offset + 5 + j] << (j * 8);

            attributes.Add(new StorageAtaSmartAttribute(
                id,
                (ushort)(sector[offset + 1] | (sector[offset + 2] << 8)),
                sector[offset + 3],
                sector[offset + 4],
                rawValue));
        }

        if (attributes.Count == 0)
            return null;

        attributes.Sort((a, b) => a.Id.CompareTo(b.Id));
        StorageAtaSmartAttribute? Find(byte id)
        {
            foreach (StorageAtaSmartAttribute item in attributes)
                if (item.Id == id)
                    return item;
            return null;
        }

        // Sector-counting RAW semantics are assessed only for confirmed HDDs.
        ulong? reallocated = mediaType == 3 ? Find(0x05)?.RawValue : null;
        ulong? pending = mediaType == 3 ? Find(0xC5)?.RawValue : null;
        ulong? uncorrectable = mediaType == 3 ? Find(0xC6)?.RawValue : null;

        string? family = null;
        byte? lifeRemaining = null;
        if (mediaType == 4 && !string.IsNullOrWhiteSpace(model))
        {
            string cleanModel = model.Trim();
            foreach (DriveFamilyRule rule in FamilyRules)
            {
                if (!rule.MatchesModel(cleanModel))
                    continue;

                family = rule.Family;
                StorageAtaSmartAttribute? attribute =
                    Find(rule.RemainingLifeAttribute);
                if (attribute != null)
                    lifeRemaining = rule.ReadLifeRemaining(attribute);
                break;
            }
        }

        return new StorageAtaSmartDetails
        {
            Attributes = attributes,
            RecognizedFamily = family,
            ReallocatedSectors = reallocated,
            PendingSectors = pending,
            UncorrectableSectors = uncorrectable,
            KingstonA400LifeRemainingPercent = lifeRemaining
        };
    }
}

// The RAW value is stored exactly as a 48-bit unsigned integer. Its meaning is
// *not* standardized across ATA vendors; UI must not call it a sector count or
// remaining-life percentage without a verified interpretation rule.
public sealed record StorageAtaSmartAttribute(
    byte Id,
    ushort Flags,
    byte Current,
    byte Worst,
    ulong RawValue)
{
    // A null threshold was not obtained from the drive's firmware.
    public byte? Threshold { get; init; }
}
