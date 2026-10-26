using System.Globalization;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Secrets.Tests;

/// <summary>
/// Verifies that a <see cref="SecretString"/> renders only the redaction marker through the text paths a caller can use.
/// </summary>
public sealed class SecretStringRedactionTests
{
    private const string Value = "fixture-secret-7f3a9c2e51d84b06";

    [Fact]
    public void ToStringReturnsTheRedactionMarkerAndNeverTheValue()
    {
        var secret = new SecretString(Value);

        secret.ToString().ShouldBe(SecretString.RedactionMarker);
        secret.ToString().ShouldNotContain(Value);
    }

    [Fact]
    public void StringInterpolationShowsOnlyTheMarker()
    {
        var secret = new SecretString(Value);

        string text = $"provider key: {secret}";

        text.ShouldBe($"provider key: {SecretString.RedactionMarker}");
        text.ShouldNotContain(Value);
    }

    [Fact]
    public void StringFormatShowsOnlyTheMarkerEvenWithAFormatSpecifier()
    {
        var secret = new SecretString(Value);

        string plain = string.Format(CultureInfo.InvariantCulture, "key={0}", secret);
        string specified = string.Format(CultureInfo.InvariantCulture, "key={0:x}", secret);

        plain.ShouldBe($"key={SecretString.RedactionMarker}");
        specified.ShouldBe(plain);
        specified.ShouldNotContain(Value);
    }
}
