using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests;

/// <summary>
/// Verifies the contract version constant and header name.
/// </summary>
public sealed class ContractVersionTests
{
    // SemVer 2.0.0 grammar (https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string).
    private static readonly Regex SemVer2 = new(
        @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<prerelease>(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<build>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void CurrentIsSemVer2StringWithDeclaredComponents()
    {
        var match = SemVer2.Match(ContractVersion.Current);

        match.Success.ShouldBeTrue();
        match.Groups["major"].Value.ShouldBe("1");
        match.Groups["minor"].Value.ShouldBe("3");
        match.Groups["patch"].Value.ShouldBe("0");
    }

    [Fact]
    public void HeaderNameIsStable()
    {
        ContractVersion.HeaderName.ShouldBe("X-Ompos-Contract-Version");
    }
}
