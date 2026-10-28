using OmpOS.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The footer version text comes from an assembly informational version, shortened to the version plus a short commit.</summary>
public sealed class ShellInfoTests
{
    /// <summary>Informational versions format as the version plus at most seven metadata characters, and fall back to dev.</summary>
    [Theory]
    [InlineData("0.15.0", "0.15.0")]
    [InlineData("0.15.0+0123456789abcdef", "0.15.0+0123456")]
    [InlineData("0.15.0+abc", "0.15.0+abc")]
    [InlineData("0.15.0-preview.1+abcdef0", "0.15.0-preview.1+abcdef0")]
    [InlineData("0.15.0+", "0.15.0")]
    [InlineData("  0.15.0  ", "0.15.0")]
    [InlineData("", "dev")]
    [InlineData("   ", "dev")]
    [InlineData(null, "dev")]
    public void FormatVersionKeepsTheVersionAndAShortCommit(string? informational, string expected)
    {
        ShellInfo.FormatVersion(informational).ShouldBe(expected);
    }

    /// <summary>The source offer names the repository and the licence the project ships under.</summary>
    [Fact]
    public void SourceOfferNamesTheRepositoryAndTheLicence()
    {
        ShellInfo.SourceRepositoryUrl.ShouldBe("https://github.com/swcstudiospace/OmpOS");
        ShellInfo.LicenceId.ShouldBe("AGPL-3.0-or-later");
    }
}
