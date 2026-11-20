using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace PCHardwareMonitor;

public sealed partial class StorageAnalyzerService
{


    private static void ParseRecord(
        Span<byte> record,
        ulong recordNumber,
        int bytesPerSector,
        int bytesPerCluster,
        Dictionary<ulong, Entry> entries,
        ParseStats stats)
    {
        if (!HasFileSignature(record))
            return;

        if (!ApplyUpdateSequenceFixup(
                record,
                bytesPerSector))
        {
            stats.FixupErrors++;
            return;
        }

        if (record.Length < 48)
            return;

        ushort flags =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x16, 2));

        if ((flags & FileRecordInUse) == 0)
            return;

        ulong baseReferenceRaw =
            BinaryPrimitives.ReadUInt64LittleEndian(
                record.Slice(0x20, 8));

        ulong baseRecordNumber =
            GetRecordNumber(baseReferenceRaw);

        bool isExtension =
            baseRecordNumber != 0;

        ulong ownerRecordNumber =
            isExtension
                ? baseRecordNumber
                : recordNumber;

        if (!entries.TryGetValue(
                ownerRecordNumber,
                out Entry? entry))
        {
            entry = new Entry(ownerRecordNumber);
            entries.Add(ownerRecordNumber, entry);
        }

        if (!isExtension)
        {
            if (!entry.HasBaseRecord)
            {
                if ((flags & FileRecordDirectory) != 0)
                    stats.FoldersFound++;
                else
                    stats.FilesFound++;
            }

            entry.HasBaseRecord = true;
            entry.IsDirectory =
                (flags & FileRecordDirectory) != 0;
        }

        ushort firstAttributeOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x14, 2));

        uint bytesInUseRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                record.Slice(0x18, 4));

        int bytesInUse =
            (int)Math.Min(
                bytesInUseRaw,
                (uint)record.Length);

        if (firstAttributeOffset >= bytesInUse)
            return;

        int offset = firstAttributeOffset;

        while (offset + 16 <= bytesInUse)
        {
            uint type =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset, 4));

            if (type == AttributeEnd)
                break;

            uint lengthRaw =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset + 4, 4));

            if (lengthRaw < 16 ||
                lengthRaw > int.MaxValue)
            {
                break;
            }

            int attributeLength = (int)lengthRaw;

            if (offset + attributeLength > bytesInUse)
                break;

            ReadOnlySpan<byte> attribute =
                record.Slice(
                    offset,
                    attributeLength);

            byte formCode = attribute[8];
            byte nameLength = attribute[9];

            ushort attributeFlags =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    attribute.Slice(12, 2));

            if (type == AttributeTypeAttributeList)
            {
                entry.HasAttributeList = true;
            }
            else if (type == AttributeTypeFileName &&
                     formCode == 0)
            {
                if (TryReadFileName(
                        attribute,
                        out string? name,
                        out ulong parentRecord,
                        out byte nameSpace))
                {
                    int priority =
                        GetNamePriority(nameSpace);

                    if (priority > entry.NamePriority)
                    {
                        entry.Name = name ?? "";
                        entry.ParentRecord = parentRecord;
                        entry.NamePriority = priority;
                    }
                }
            }
            else if (type == AttributeTypeData &&
                     nameLength == 0)
            {
                if (formCode == 0)
                {
                    if (!entry.HasUnnamedData &&
                        TryReadResidentDataSize(
                            attribute,
                            out long logicalSize))
                    {
                        entry.HasUnnamedData = true;
                        entry.IsResidentData = true;
                        entry.LogicalSize = logicalSize;
                        entry.AllocatedSize = 0;
                    }
                }
                else if (formCode == 1 &&
                         TryReadNonResidentDataExtent(
                             attribute,
                             attributeFlags,
                             bytesPerCluster,
                             out long lowestVcn,
                             out long logicalSize,
                             out long extentAllocatedSize,
                             out bool sparse,
                             out bool compressed,
                             out bool encrypted))
                {
                    bool wasResident =
                        entry.HasUnnamedData &&
                        entry.IsResidentData;

                    if (!entry.HasUnnamedData || wasResident)
                    {
                        entry.HasUnnamedData = true;
                        entry.IsResidentData = false;

                        if (wasResident)
                            entry.AllocatedSize = 0;
                    }

                    entry.AllocatedSize =
                        checked(
                            entry.AllocatedSize +
                            extentAllocatedSize);

                    entry.IsSparse |= sparse;
                    entry.IsCompressed |= compressed;
                    entry.IsEncrypted |= encrypted;

                    if (lowestVcn == 0)
                        entry.LogicalSize = logicalSize;
                }
            }

            offset += attributeLength;
        }
    }

    private static bool TryReadResidentDataSize(
        ReadOnlySpan<byte> attribute,
        out long logicalSize)
    {
        logicalSize = 0;

        if (attribute.Length < 24)
            return false;

        logicalSize =
            BinaryPrimitives.ReadUInt32LittleEndian(
                attribute.Slice(16, 4));

        return true;
    }

    private static bool TryReadNonResidentDataExtent(
        ReadOnlySpan<byte> attribute,
        ushort attributeFlags,
        int bytesPerCluster,
        out long lowestVcn,
        out long logicalSize,
        out long extentAllocatedSize,
        out bool sparse,
        out bool compressed,
        out bool encrypted)
    {
        lowestVcn = 0;
        logicalSize = 0;
        extentAllocatedSize = 0;
        sparse = false;
        compressed = false;
        encrypted = false;

        if (attribute.Length < 64 ||
            bytesPerCluster <= 0)
        {
            return false;
        }

        lowestVcn =
            BinaryPrimitives.ReadInt64LittleEndian(
                attribute.Slice(16, 8));

        sparse =
            (attributeFlags & AttributeFlagSparse) != 0;

        compressed =
            (attributeFlags & AttributeFlagCompressionMask) != 0;

        encrypted =
            (attributeFlags & AttributeFlagEncrypted) != 0;

        if (lowestVcn == 0)
        {
            logicalSize =
                BinaryPrimitives.ReadInt64LittleEndian(
                    attribute.Slice(48, 8));

            if (logicalSize < 0)
                return false;
        }

        ushort mappingPairsOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                attribute.Slice(32, 2));

        if (mappingPairsOffset >= attribute.Length)
            return false;

        if (!TryParseRunList(
                attribute.Slice(mappingPairsOffset),
                lowestVcn,
                out List<DataRun> runs))
        {
            return false;
        }

        long allocatedClusters = 0;

        foreach (DataRun run in runs)
        {
            if (!run.IsSparse)
            {
                allocatedClusters =
                    checked(
                        allocatedClusters +
                        run.ClusterCount);
            }
        }

        extentAllocatedSize =
            checked(
                allocatedClusters *
                (long)bytesPerCluster);

        return true;
    }

    private static bool TryReadFileName(
        ReadOnlySpan<byte> attribute,
        out string? name,
        out ulong parentRecord,
        out byte nameSpace)
    {
        name = null;
        parentRecord = 0;
        nameSpace = 0;

        if (attribute.Length < 24)
            return false;

        uint valueLengthRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                attribute.Slice(16, 4));

        ushort valueOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                attribute.Slice(20, 2));

        if (valueLengthRaw > int.MaxValue)
            return false;

        int valueLength = (int)valueLengthRaw;

        if (valueOffset + valueLength > attribute.Length ||
            valueLength < 66)
        {
            return false;
        }

        ReadOnlySpan<byte> value =
            attribute.Slice(
                valueOffset,
                valueLength);

        ulong parentReference =
            BinaryPrimitives.ReadUInt64LittleEndian(
                value.Slice(0, 8));

        parentRecord =
            GetRecordNumber(parentReference);

        byte fileNameLength = value[64];
        nameSpace = value[65];

        int nameBytes =
            checked(fileNameLength * 2);

        if (66 + nameBytes > value.Length)
            return false;

        name =
            Encoding.Unicode.GetString(
                value.Slice(66, nameBytes));

        return true;
    }

    private static int GetNamePriority(byte nameSpace)
    {
        return nameSpace switch
        {
            3 => 4,
            1 => 3,
            0 => 2,
            2 => 1,
            _ => 0
        };
    }

    private static byte[] ReadMftBaseRecord(
        SafeFileHandle volumeHandle,
        NtfsVolumeDataBuffer volumeData,
        int bytesPerSector,
        int bytesPerCluster,
        int bytesPerRecord)
    {
        byte[] record = new byte[bytesPerRecord];

        long mftStartOffset =
            checked(
                volumeData.MftStartLcn *
                (long)bytesPerCluster);

        int bytesRead =
            RandomAccess.Read(
                volumeHandle,
                record.AsSpan(),
                mftStartOffset);

        if (bytesRead != bytesPerRecord)
        {
            throw new InvalidDataException(
                SettingsService.L($"Не удалось прочитать первую запись MFT. Прочитано {bytesRead} из {bytesPerRecord} байт.",
                    $"Unable to read the first MFT record. Read {bytesRead} of {bytesPerRecord} bytes."));
        }

        if (!HasFileSignature(record))
        {
            throw new InvalidDataException(
                SettingsService.L("В первой записи MFT отсутствует сигнатура FILE.", "The first MFT record has no FILE signature."));
        }

        if (!ApplyUpdateSequenceFixup(
                record,
                bytesPerSector))
        {
            throw new InvalidDataException(
                SettingsService.L("Не удалось проверить и восстановить последовательность обновления записи MFT.", "Unable to apply NTFS update sequence fixup to the MFT record."));
        }

        return record;
    }

    private static MftDataInfo GetMftRunList(
        ReadOnlySpan<byte> record)
    {
        ushort firstAttributeOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(0x14, 2));

        uint bytesInUseRaw =
            BinaryPrimitives.ReadUInt32LittleEndian(
                record.Slice(0x18, 4));

        int bytesInUse =
            (int)Math.Min(
                bytesInUseRaw,
                (uint)record.Length);

        int offset = firstAttributeOffset;

        while (offset + 16 <= bytesInUse)
        {
            uint type =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset, 4));

            if (type == AttributeEnd)
                break;

            uint lengthRaw =
                BinaryPrimitives.ReadUInt32LittleEndian(
                    record.Slice(offset + 4, 4));

            if (lengthRaw < 16 ||
                lengthRaw > int.MaxValue)
            {
                break;
            }

            int attributeLength = (int)lengthRaw;

            if (offset + attributeLength > bytesInUse)
                break;

            byte formCode = record[offset + 8];
            byte nameLength = record[offset + 9];

            if (type == AttributeTypeData &&
                formCode == 1 &&
                nameLength == 0)
            {
                if (attributeLength < 64)
                    break;

                long lowestVcn =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 16, 8));

                long highestVcn =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 24, 8));

                ushort mappingPairsOffset =
                    BinaryPrimitives.ReadUInt16LittleEndian(
                        record.Slice(offset + 32, 2));

                long fileSize =
                    BinaryPrimitives.ReadInt64LittleEndian(
                        record.Slice(offset + 48, 8));

                if (mappingPairsOffset >= attributeLength)
                    break;

                if (!TryParseRunList(
                        record.Slice(
                            offset + mappingPairsOffset,
                            attributeLength - mappingPairsOffset),
                        lowestVcn,
                        out List<DataRun> runs))
                {
                    break;
                }

                return new MftDataInfo(
                    lowestVcn,
                    highestVcn,
                    fileSize,
                    runs);
            }

            offset += attributeLength;
        }

        throw new InvalidDataException(
            SettingsService.L("Не удалось найти безымянный нерезидентный атрибут $DATA таблицы $MFT.", "Unable to locate the unnamed nonresident $DATA attribute of $MFT."));
    }

    private static bool TryParseRunList(
        ReadOnlySpan<byte> mappingPairs,
        long startingVcn,
        out List<DataRun> runs)
    {
        runs = new List<DataRun>();

        int offset = 0;
        long currentVcn = startingVcn;
        long currentLcn = 0;

        while (offset < mappingPairs.Length)
        {
            byte header = mappingPairs[offset++];

            if (header == 0)
                return true;

            int lengthBytes = header & 0x0F;
            int offsetBytes = (header >> 4) & 0x0F;

            if (lengthBytes == 0 ||
                lengthBytes > 8 ||
                offsetBytes > 8 ||
                offset + lengthBytes + offsetBytes > mappingPairs.Length)
            {
                return false;
            }

            long clusterCount =
                ReadUnsignedLittleEndian(
                    mappingPairs.Slice(
                        offset,
                        lengthBytes));

            offset += lengthBytes;

            if (clusterCount <= 0)
                return false;

            bool isSparse = offsetBytes == 0;
            long startLcn = -1;

            if (!isSparse)
            {
                long lcnDelta =
                    ReadSignedLittleEndian(
                        mappingPairs.Slice(
                            offset,
                            offsetBytes));

                currentLcn =
                    checked(
                        currentLcn +
                        lcnDelta);

                startLcn = currentLcn;
            }

            offset += offsetBytes;

            runs.Add(
                new DataRun(
                    currentVcn,
                    clusterCount,
                    startLcn,
                    isSparse));

            currentVcn =
                checked(
                    currentVcn +
                    clusterCount);
        }

        return false;
    }

    private static bool HasFileSignature(
        ReadOnlySpan<byte> record)
    {
        return record.Length >= 4 &&
               record[0] == (byte)'F' &&
               record[1] == (byte)'I' &&
               record[2] == (byte)'L' &&
               record[3] == (byte)'E';
    }

    private static bool ApplyUpdateSequenceFixup(
        Span<byte> record,
        int bytesPerSector)
    {
        if (record.Length < 8 ||
            bytesPerSector <= 0)
        {
            return false;
        }

        ushort usaOffset =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(4, 2));

        ushort usaCount =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(6, 2));

        if (usaCount < 2)
            return false;

        int usaBytes =
            checked(usaCount * 2);

        if (usaOffset + usaBytes > record.Length)
            return false;

        ushort sequenceNumber =
            BinaryPrimitives.ReadUInt16LittleEndian(
                record.Slice(usaOffset, 2));

        int sectorsInRecord =
            record.Length / bytesPerSector;

        if (usaCount != sectorsInRecord + 1)
            return false;

        for (int sectorIndex = 1;
             sectorIndex < usaCount;
             sectorIndex++)
        {
            int sectorEnd =
                checked(
                    sectorIndex *
                    bytesPerSector -
                    2);

            ushort onDiskValue =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    record.Slice(sectorEnd, 2));

            if (onDiskValue != sequenceNumber)
                return false;

            ushort replacement =
                BinaryPrimitives.ReadUInt16LittleEndian(
                    record.Slice(
                        usaOffset +
                        sectorIndex * 2,
                        2));

            BinaryPrimitives.WriteUInt16LittleEndian(
                record.Slice(sectorEnd, 2),
                replacement);
        }

        return true;
    }

    private static ulong GetRecordNumber(
        ulong fileReference)
    {
        return fileReference &
               0x0000FFFFFFFFFFFFUL;
    }

    private static long ReadUnsignedLittleEndian(
        ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            value |=
                (ulong)bytes[i] <<
                (8 * i);
        }

        if (value > long.MaxValue)
            throw new OverflowException();

        return (long)value;
    }

    private static long ReadSignedLittleEndian(
        ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            value |=
                (ulong)bytes[i] <<
                (8 * i);
        }

        int bits = bytes.Length * 8;

        if (bits < 64 &&
            (bytes[^1] & 0x80) != 0)
        {
            value |=
                ulong.MaxValue << bits;
        }

        return unchecked((long)value);
    }

    private sealed class ParseStats
    {
        public long FixupErrors;
        public long FilesFound;
        public long FoldersFound;
    }


    private readonly record struct DataRun(
        long StartVcn,
        long ClusterCount,
        long StartLcn,
        bool IsSparse);

    private readonly record struct MftDataInfo(
        long LowestVcn,
        long HighestVcn,
        long FileSize,
        List<DataRun> Runs);

}
