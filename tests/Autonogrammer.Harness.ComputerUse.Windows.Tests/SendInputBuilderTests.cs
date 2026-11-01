using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Windows.Tests;

/// <summary>Absolute normalisation, Unicode text and scan-code chords. No P/Invoke.</summary>
public sealed class SendInputBuilderTests
{
    [Fact]
    public void OriginIsZeroAndFarCornerIs65535()
    {
        SendInputBuilder.AbsolutePoint origin = SendInputBuilder.NormalizeAbsolute(100, -40, 100, -40, 1920, 1080);
        origin.X.ShouldBe(0);
        origin.Y.ShouldBe(0);

        SendInputBuilder.AbsolutePoint far = SendInputBuilder.NormalizeAbsolute(100 + 1919, -40 + 1079, 100, -40, 1920, 1080);
        far.X.ShouldBe(65535);
        far.Y.ShouldBe(65535);
    }

    [Fact]
    public void OutOfRangePointsClampInsteadOfWrapping()
    {
        SendInputBuilder.AbsolutePoint low = SendInputBuilder.NormalizeAbsolute(-5000, -8000, 0, 0, 800, 600);
        low.X.ShouldBe(0);
        low.Y.ShouldBe(0);

        SendInputBuilder.AbsolutePoint high = SendInputBuilder.NormalizeAbsolute(50_000, 90_000, 0, 0, 800, 600);
        high.X.ShouldBe(65535);
        high.Y.ShouldBe(65535);
        high.X.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TextIsUnicodeDownUpPairs()
    {
        SendInputBuilder.InputRecord[] records = SendInputBuilder.EncodeText("Aé");
        records.Length.ShouldBe(4);
        records[0].Type.ShouldBe(SendInputBuilder.InputKeyboard);
        records[0].Flags.ShouldBe(SendInputBuilder.KeyEventFUnicode);
        records[0].Scan.ShouldBe((ushort)'A');
        records[1].Flags.ShouldBe(SendInputBuilder.KeyEventFUnicode | SendInputBuilder.KeyEventFKeyUp);
        records[1].Scan.ShouldBe((ushort)'A');
        records[2].Scan.ShouldBe((ushort)'é');
        records[2].Flags.ShouldBe(SendInputBuilder.KeyEventFUnicode);
        records[3].Flags.ShouldBe(SendInputBuilder.KeyEventFUnicode | SendInputBuilder.KeyEventFKeyUp);
    }

    [Fact]
    public void ChordIsScanCodeDownUpPairsInOrder()
    {
        ushort[] scans = [0x1D, 0x38, 0x01];
        SendInputBuilder.InputRecord[] records = SendInputBuilder.EncodeChord(scans);
        records.Length.ShouldBe(6);
        records[0].Scan.ShouldBe((ushort)0x1D);
        records[0].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode);
        records[1].Scan.ShouldBe((ushort)0x1D);
        records[1].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode | SendInputBuilder.KeyEventFKeyUp);
        records[2].Scan.ShouldBe((ushort)0x38);
        records[2].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode);
        records[3].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode | SendInputBuilder.KeyEventFKeyUp);
        records[4].Scan.ShouldBe((ushort)0x01);
        records[4].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode);
        records[5].Scan.ShouldBe((ushort)0x01);
        records[5].Flags.ShouldBe(SendInputBuilder.KeyEventFScanCode | SendInputBuilder.KeyEventFKeyUp);
        records[0].Type.ShouldBe(SendInputBuilder.InputKeyboard);
    }
}
