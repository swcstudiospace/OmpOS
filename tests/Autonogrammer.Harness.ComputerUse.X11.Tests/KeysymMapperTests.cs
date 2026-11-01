using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.X11.Tests;

/// <summary>Pure keysym mapping, including the scratch-keycode restore record.</summary>
public sealed class KeysymMapperTests
{
    [Fact]
    public void AsciiCharactersMapToTheirKeysyms()
    {
        KeysymMapper.ToKeysym('A').ShouldBe(0x41u);
        KeysymMapper.ToKeysym('a').ShouldBe(0x61u);
        KeysymMapper.ToKeysym(' ').ShouldBe(0x20u);
        KeysymMapper.ToKeysym(',').ShouldBe(0x2cu);
    }

    [Fact]
    public void UnicodeCharactersMapToTheUnicodePlane()
    {
        const char diaeresisI = '\u00ef';
        KeysymMapper.ToKeysym(diaeresisI).ShouldBe(KeysymMapper.UnicodePlane | diaeresisI);
        KeysymMapper.ToKeysym('\u00fc').ShouldBe(0x010000fcu);
        KeysymMapper.ToKeysym('\u4e16').ShouldBe(0x01004e16u);
    }

    [Fact]
    public void UnmappedCharacterTakesTheScratchPathAndRecordsRestoreAfterXSync()
    {
        KeysymPlan plan = KeysymMapper.Plan('\u00ef', _ => false);

        plan.UsesScratchKeycode.ShouldBeTrue();
        plan.RestoreAfterXSync.ShouldBeTrue();
        plan.Keysym.ShouldBe(KeysymMapper.UnicodePlane | '\u00ef');
    }

    [Fact]
    public void MappedCharacterDoesNotRemap()
    {
        KeysymPlan plan = KeysymMapper.Plan('a', keysym => keysym == 'a');

        plan.UsesScratchKeycode.ShouldBeFalse();
        plan.RestoreAfterXSync.ShouldBeFalse();
    }
}
