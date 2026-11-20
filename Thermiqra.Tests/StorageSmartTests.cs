using System.Buffers.Binary;
using System.Numerics;
using PCHardwareMonitor;
using Xunit;

namespace Thermiqra.Tests;

public class StorageSmartTests
{
    private static byte[] Ata(byte id, ulong raw = 0, byte current = 100)
    {
        byte[] sector = new byte[512];
        sector[2] = id; sector[5] = current; sector[6] = 90;
        for (int i = 0; i < 6; i++) sector[7 + i] = (byte)(raw >> (8 * i));
        return sector;
    }

    [Fact] public void AtaMissingDataIsUnknown()
    {
        Assert.Null(StorageAtaSmartInterpreter.Parse(null!, null, null));
        Assert.Null(StorageAtaSmartInterpreter.Parse(new byte[361], null, null));
        Assert.Null(StorageAtaSmartInterpreter.Parse(new byte[512], null, null));
    }

    [Fact] public void AtaDuplicateIdsInvalidateTable()
    {
        var data = Ata(5); data[14] = 5;
        Assert.Null(StorageAtaSmartInterpreter.Parse(data, "Example HDD", 3));
    }

    [Fact] public void AtaRawCounterIsNotTruncatedOrUsedAsNormalizedValue()
    {
        var smart = StorageAtaSmartInterpreter.Parse(Ata(5, 0xFEDCBA987654), null, 3)!;
        Assert.Equal(0xFEDCBA987654UL, smart.ReallocatedSectors);
        Assert.Null(smart.PendingSectors);
        Assert.Null(smart.UncorrectableSectors);
        Assert.Null(smart.Attributes[0].Threshold);
    }

    [Theory] [InlineData("Unknown SSD", 4)] [InlineData("KINGSTON SA400S37480G", 3)]
    public void VendorLifeRequiresBothRecognizedFamilyAndSsd(string model, ushort media)
    {
        var smart = StorageAtaSmartInterpreter.Parse(Ata(0xE7, 87, 12), model, media)!;
        Assert.Null(smart.KingstonA400LifeRemainingPercent);
    }

    [Theory] [InlineData(87, 87)] [InlineData(101, null)] [InlineData(65535, null)]
    public void KingstonLifeUsesDocumentedRawField(int raw, int? expected)
    {
        var smart = StorageAtaSmartInterpreter.Parse(Ata(0xE7, (ulong)raw, 12), "KINGSTON SA400S37480G", 4)!;
        Assert.Equal(expected, smart.KingstonA400LifeRemainingPercent is byte life ? (int?)life : null);
        Assert.Null(smart.ReallocatedSectors);
    }

    [Fact] public void AtaThresholdsRejectDuplicateAndZeroWithoutChangingAttributes()
    {
        var smart = StorageAtaSmartInterpreter.Parse(Ata(5, 9), null, 3)!;
        var thresholds = new byte[512]; thresholds[2] = 5;
        Assert.False(StorageAtaSmartInterpreter.TryApplyThresholds(smart, thresholds));
        thresholds[3] = 36; thresholds[14] = 5;
        Assert.False(StorageAtaSmartInterpreter.TryApplyThresholds(smart, thresholds));
        Assert.Null(smart.Attributes[0].Threshold);
        thresholds[14] = 0;
        Assert.True(StorageAtaSmartInterpreter.TryApplyThresholds(smart, thresholds));
        Assert.Equal((byte)36, smart.Attributes[0].Threshold);
        Assert.Equal(9UL, smart.Attributes[0].RawValue);
    }

    private static byte[] Nvme()
    {
        byte[] data = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(1), 300);
        data[3] = 100; data[4] = 10;
        return data;
    }

    [Fact] public void NvmeInvalidLayoutsCannotBecomeHealthyZeroes()
    {
        Assert.Null(StorageNvmeHealthReader.ParseHealthLog(new byte[511]));
        Assert.Null(StorageNvmeHealthReader.ParseHealthLog(new byte[512]));
        var data = Nvme(); data[3] = 101;
        Assert.Null(StorageNvmeHealthReader.ParseHealthLog(data));
    }

    [Fact] public void NvmePreservesLargeUnsignedCountersWarningsAndWearOver100()
    {
        var data = Nvme(); data[0] = 4; data[5] = 200; data[175] = 0x80;
        var smart = StorageNvmeHealthReader.ParseHealthLog(data)!;
        Assert.Equal(BigInteger.One << 127, smart.MediaErrors);
        Assert.Equal(4, smart.CriticalWarning);
        Assert.Equal(200, smart.PercentageUsed);
        Assert.Equal(27, smart.CompositeTemperatureCelsius);
    }
}
