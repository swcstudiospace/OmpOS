using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.McpGateway.Tests;

/// <summary>The bearer secret is random per gateway and checked exactly.</summary>
public sealed class McpGatewayBearerTokenTests
{
    private const string Secret = "abc-123_secret-value";

    [Fact]
    public void GeneratedSecretsAreUniqueUrlSafeAndLongEnough()
    {
        var secrets = Enumerable.Range(0, 32).Select(static _ => McpGatewayBearerToken.Generate()).ToList();

        secrets.Distinct(StringComparer.Ordinal).Count().ShouldBe(secrets.Count);
        foreach (var secret in secrets)
        {
            secret.Length.ShouldBeGreaterThanOrEqualTo(43);
            secret.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData("Bearer abc-123_secret-value", true)]
    [InlineData("bearer abc-123_secret-value", true)]
    [InlineData("BEARER abc-123_secret-value", true)]
    [InlineData("Bearer  abc-123_secret-value ", true)]
    [InlineData("Bearer abc-123_secret-valu", false)]
    [InlineData("Bearer abc-123_secret-value2", false)]
    [InlineData("Bearer ABC-123_SECRET-VALUE", false)]
    [InlineData("Basic abc-123_secret-value", false)]
    [InlineData("abc-123_secret-value", false)]
    [InlineData("Bearer", false)]
    [InlineData("Bearer ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HeaderMatchesOnlyTheBearerSchemeWithTheExactSecret(string? header, bool expected)
    {
        McpGatewayBearerToken.Matches(Secret, header).ShouldBe(expected);
    }

    [Fact]
    public void HeaderValueRoundTripsThroughMatches()
    {
        McpGatewayBearerToken.Matches(Secret, McpGatewayBearerToken.HeaderValue(Secret)).ShouldBeTrue();
    }

    [Fact]
    public void AnEmptyExpectedSecretIsRejectedAsAProgrammingError()
    {
        Should.Throw<ArgumentException>(() => McpGatewayBearerToken.Matches(string.Empty, "Bearer "));
    }
}
