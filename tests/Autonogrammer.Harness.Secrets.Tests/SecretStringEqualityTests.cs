using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>
/// Verifies value equality of a <see cref="SecretString"/> through each equality path, and that unequal secrets stay unequal.
/// </summary>
public sealed class SecretStringEqualityTests
{
    [Fact]
    public void EqualValuesCompareEqualThroughEveryEqualityPath()
    {
        var first = new SecretString("fixture-secret-7f3a");
        var second = new SecretString("fixture-secret-7f3a");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object)second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ASecretEqualsItselfAndNothingElse()
    {
        var secret = new SecretString("fixture-secret-7f3a");

        secret.Equals(secret).ShouldBeTrue();
        SecretString? absent = SecretOrNull(null);
        secret.Equals(absent).ShouldBeFalse();
    }

    [Fact]
    public void ValuesDifferingInOneCharacterPrefixOrCaseAreNotEqual()
    {
        var secret = new SecretString("fixture-secret-7f3a");

        secret.Equals(new SecretString("fixture-secret-7f3b")).ShouldBeFalse();
        secret.Equals(new SecretString("fixture-secret-7f3")).ShouldBeFalse();
        secret.Equals(new SecretString("FIXTURE-SECRET-7F3A")).ShouldBeFalse();
    }

    [Fact]
    public void ASecretIsNotEqualToItsPlainTextOrToOtherTypes()
    {
        var secret = new SecretString("fixture-secret-7f3a");

        secret.Equals((object)"fixture-secret-7f3a").ShouldBeFalse();
    }

    [Fact]
    public void UnpairedSurrogateIsNotEqualToReplacementCharacter()
    {
        var lone = new SecretString("\uD800");
        var replacement = new SecretString("\uFFFD");

        lone.Equals(replacement).ShouldBeFalse();
        replacement.Equals(lone).ShouldBeFalse();
        lone.Equals((object)replacement).ShouldBeFalse();
    }

    [Fact]
    public void ValuesWithIdenticalCodeUnitsRemainEqualIncludingSurrogates()
    {
        new SecretString("\uD800").Equals(new SecretString("\uD800")).ShouldBeTrue();
        new SecretString("\uD83D\uDE00").Equals(new SecretString("\uD83D\uDE00")).ShouldBeTrue();
    }

    [Fact]
    public void HashCodeDoesNotDependOnTheLengthOfTheValue()
    {
        var shortSecret = new SecretString("a");
        var longSecret = new SecretString("a considerably longer fixture-secret");

        shortSecret.GetHashCode().ShouldBe(longSecret.GetHashCode());
    }

    private static SecretString? SecretOrNull(string? text) => text is null ? null : new SecretString(text);
}
