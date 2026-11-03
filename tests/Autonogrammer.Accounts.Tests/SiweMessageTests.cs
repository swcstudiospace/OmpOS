using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// The Sign-In-With-Ethereum message a wallet signs: its EIP-4361 layout, the UTC issue time, and the refusal of every malformed field before a
/// signature is requested.
/// </summary>
public sealed class SiweMessageTests
{
    private const string Address = "0x1234567890123456789012345678901234567890";
    private static readonly DateTimeOffset Issued = new(2026, 11, 3, 12, 0, 0, 123, TimeSpan.Zero);

    [Fact]
    public void BuildsTheLayoutWithTheStatementBetweenTheAddressAndTheFields()
    {
        var fields = new SiweFields("example.org", Address, new Uri("https://example.org/connect"), "1", "abcdefgh12", Issued, "Sign in to Autonogrammer.");

        SiweMessage.Build(fields).ShouldBe(
            "example.org wants you to sign in with your Ethereum account:\n" + Address + "\n\nSign in to Autonogrammer.\n\n"
            + "URI: https://example.org/connect\nVersion: 1\nChain ID: 1\nNonce: abcdefgh12\nIssued At: 2026-11-03T12:00:00.123Z");
    }

    [Fact]
    public void BuildsTheLayoutWithoutAStatementAndKeepsAPortInTheDomain()
    {
        var fields = new SiweFields("example.org:8443", Address, new Uri("https://example.org:8443/connect"), "1", "abcdefgh12", Issued, null);

        SiweMessage.Build(fields).ShouldBe(
            "example.org:8443 wants you to sign in with your Ethereum account:\n" + Address + "\n\n"
            + "URI: https://example.org:8443/connect\nVersion: 1\nChain ID: 1\nNonce: abcdefgh12\nIssued At: 2026-11-03T12:00:00.123Z");
    }

    [Fact]
    public void WritesTheIssueTimeInUtcWhateverTheOffset()
    {
        var local = new DateTimeOffset(2026, 11, 3, 14, 0, 0, 0, TimeSpan.FromHours(2));
        var fields = new SiweFields("example.org", Address, new Uri("https://example.org"), "1", "abcdefgh12", local, null);

        SiweMessage.Build(fields).ShouldEndWith("Issued At: 2026-11-03T12:00:00.000Z");
    }

    [Theory]
    [InlineData("0x123")]
    [InlineData("1234567890123456789012345678901234567890")]
    [InlineData("0xZZ34567890123456789012345678901234567890")]
    public void RefusesAnAddressThatIsNotTwentyBytesOfHex(string address) =>
        RefusesNaming(Valid() with { Address = address }, "address");

    [Theory]
    [InlineData("abc")]
    [InlineData("abc-defgh")]
    [InlineData("")]
    public void RefusesANonceThatIsShortOrNotAlphanumeric(string nonce) =>
        RefusesNaming(Valid() with { Nonce = nonce }, "nonce");

    [Theory]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("abc")]
    public void RefusesAChainIdThatIsNotAPositiveDecimal(string chainId) =>
        RefusesNaming(Valid() with { ChainId = chainId }, "chain");

    [Theory]
    [InlineData("example.org/evil")]
    [InlineData("exa mple.org")]
    public void RefusesADomainThatIsNotAnAuthority(string domain) =>
        RefusesNaming(Valid() with { Domain = domain }, "domain");

    [Fact]
    public void RefusesAPlainHttpUriOffLoopback() =>
        RefusesNaming(Valid() with { Uri = new Uri("http://example.org/connect") }, "URI");

    [Fact]
    public void RefusesAStatementThatSpansTwoLines() =>
        RefusesNaming(Valid() with { Statement = "line one\nline two" }, "statement");

    private static SiweFields Valid() => new("example.org", Address, new Uri("https://example.org/connect"), "1", "abcdefgh12", Issued, "Sign in.");

    private static void RefusesNaming(SiweFields fields, string named)
    {
        ArgumentException refusal = Should.Throw<ArgumentException>(() => SiweMessage.Build(fields));
        refusal.Message.ShouldContain(named, Case.Insensitive);
    }
}
