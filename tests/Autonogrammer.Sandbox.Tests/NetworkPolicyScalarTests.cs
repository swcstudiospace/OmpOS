using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Pins how network rule values render as YAML scalars: plain only when unambiguous, quoted and escaped otherwise.
/// </summary>
public sealed class NetworkPolicyScalarTests
{
    /// <summary>Reserved words and a bare dash would not stay strings in YAML, so they are quoted.</summary>
    [Theory]
    [InlineData("true")]
    [InlineData("No")]
    [InlineData("-")]
    public void ReservedOrAmbiguousValues_AreQuoted(string value)
    {
        NetworkPolicyRule.ToYamlScalar(value).ShouldBe($"\"{value}\"");
    }

    /// <summary>Control characters have no plain or simple escape, so they are written as unicode escapes.</summary>
    [Fact]
    public void ControlCharacters_AreWrittenAsUnicodeEscapes()
    {
        NetworkPolicyRule.ToYamlScalar("a\u0001b").ShouldBe("\"a\\u0001b\"");
    }

    /// <summary>A plain host name stays unquoted so generated policies stay readable.</summary>
    [Fact]
    public void SafeHostName_StaysPlain()
    {
        NetworkPolicyRule.ToYamlScalar("api.example.com").ShouldBe("api.example.com");
    }
}
