using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests;

/// <summary>
/// Verifies the contract version constant and header name.
/// </summary>
public sealed class ContractVersionTests
{
    [Fact]
    public void CurrentIsThreePartSemver()
    {
        Version.TryParse(ContractVersion.Current, out var version).ShouldBeTrue();
        version.ShouldNotBeNull();
        version.Build.ShouldBeGreaterThanOrEqualTo(0);
        version.Revision.ShouldBe(-1);
    }

    [Fact]
    public void HeaderNameIsStable()
    {
        ContractVersion.HeaderName.ShouldBe("X-Ompos-Contract-Version");
    }
}
