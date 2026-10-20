using System.Text.RegularExpressions;
using OmpOS.Contracts;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests;

/// <summary>
/// Verifies the contract version constant and header name.
/// </summary>
public sealed class ContractVersionTests
{
    [Fact]
    public void CurrentMatchesSemver()
    {
        Regex.IsMatch(ContractVersion.Current, @"^\d+\.\d+\.\d+$").ShouldBeTrue();
    }

    [Fact]
    public void HeaderNameIsStable()
    {
        ContractVersion.HeaderName.ShouldBe("X-Ompos-Contract-Version");
    }
}
