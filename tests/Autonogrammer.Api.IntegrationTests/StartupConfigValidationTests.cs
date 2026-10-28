using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Fail-fast configuration: unusable rate-limit or harness settings stop startup, and the error names the setting.
/// </summary>
public sealed class StartupConfigValidationTests
{
    /// <summary>Verifies each unusable setting fails startup with its key in the error.</summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The unusable value.</param>
    [Theory]
    [InlineData("Api:RateLimit:PromptsPerMinute", "0")]
    [InlineData("Api:RateLimit:SessionsPerMinute", "0")]
    [InlineData("Api:RateLimit:SessionsPerMinute", "100001")]
    [InlineData("Harness:Adapter", "Bogus")]
    public void UnusableSetting_FailsStartupNamingKey(string key, string value)
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { [key] = value });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        Flatten(caught).ShouldContain(key);
    }

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }
}
