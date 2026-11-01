using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Abstractions;

/// <summary>Validator names keys, never values, and skips checks while the feature is off.</summary>
public sealed class ComputerUseOptionsValidatorTests
{
    private const int DistinctiveValue = 424242;

    [Fact]
    public void NamesTheKeyAndNotTheValue()
    {
        ComputerUseOptions options = Enabled();
        options.RatePerMinute = DistinctiveValue;
        string joined = string.Join('\n', ComputerUseOptionsValidator.Validate(options));
        joined.ShouldContain("Cowork:ComputerUse:RatePerMinute");
        joined.ShouldNotContain(DistinctiveValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ChordNeedsTwoModifiers()
    {
        ComputerUseOptions options = Enabled();
        options.KillChord = "Ctrl+Escape";
        string joined = string.Join('\n', ComputerUseOptionsValidator.Validate(options));
        joined.ShouldContain("Cowork:ComputerUse:KillChord");
        joined.ShouldContain("two modifiers");
        joined.ShouldNotContain("Ctrl+Escape");
    }

    [Fact]
    public void ForbiddenChordIsRefused()
    {
        ComputerUseOptions options = Enabled();
        options.KillChord = "Win+R";
        string joined = string.Join('\n', ComputerUseOptionsValidator.Validate(options));
        joined.ShouldContain("Cowork:ComputerUse:KillChord");
        joined.ShouldContain("not an allowed chord");
        joined.ShouldNotContain("Win+R");
    }

    [Fact]
    public void EmptyWhenDisabled()
    {
        ComputerUseOptions options = new()
        {
            Enabled = false,
            RatePerMinute = DistinctiveValue,
            KillChord = "Win+R",
        };
        ComputerUseOptionsValidator.Validate(options).ShouldBeEmpty();
    }

    private static ComputerUseOptions Enabled() => new() { Enabled = true };
}
