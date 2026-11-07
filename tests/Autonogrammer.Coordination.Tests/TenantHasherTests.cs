using Autonogrammer.Coordination.Dragonfly;
using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>The tenant hash is a keyed, stable 32-hex value of the account id. The peppers here are throwaway test values.</summary>
public sealed class TenantHasherTests
{
    private static TenantHasher Hasher(string pepper) => new(new TenantHasherOptions { Pepper = pepper });

    [Fact]
    public void TheSameAccountAndPepperGiveTheSameLowercaseHex32()
    {
        var hash = Hasher("pepper-one").HashTenant("account-1");

        hash.ShouldBe(Hasher("pepper-one").HashTenant("account-1"));
        hash.Length.ShouldBe(32);
        hash.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f').ShouldBeTrue();
    }

    [Fact]
    public void ADifferentPepperGivesADifferentHash()
    {
        string.Equals(Hasher("pepper-one").HashTenant("account-1"), Hasher("pepper-two").HashTenant("account-1"), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void ADifferentAccountGivesADifferentHash()
    {
        string.Equals(Hasher("pepper-one").HashTenant("account-1"), Hasher("pepper-one").HashTenant("account-2"), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void AnEmailIsHashedLikeAnyOtherStringAndNeverAppearsInTheResult()
    {
        var hasher = Hasher("pepper-one");
        var hash = hasher.HashTenant("someone@example.test");

        hash.Contains("someone", StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(hash, hasher.HashTenant("SOMEONE@example.test"), StringComparison.Ordinal).ShouldBeFalse("no case folding or email handling is applied");
        hash.Length.ShouldBe(32);
    }

    [Fact]
    public void TheHashIsAcceptedAsATenantSegment()
    {
        DragonflyKeyspace.RequireTenant(Hasher("pepper-one").HashTenant("account-1")).Length.ShouldBe(32);
    }

    [Fact]
    public void ABlankPepperOrAccountIsRefused()
    {
        Should.Throw<ArgumentException>(() => Hasher(" "));
        Should.Throw<ArgumentException>(() => Hasher(string.Empty));
        Should.Throw<ArgumentException>(() => Hasher("pepper-one").HashTenant(" "));
    }
}
