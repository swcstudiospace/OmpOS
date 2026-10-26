using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// API-03 log scopes and prompt redaction defaults. A warning written while a request is handled carries that request's correlation
/// id as a log scope. Prompt text is never logged by default, because <c>OmpOS:Debug:LogPrompts</c> defaults to false.
/// </summary>
public sealed class CorrelationLogScopeTests
{
    private const string CorrelationHeader = "x-correlation-id";

    /// <summary>Verifies the rate-limit warning for a request carries that request's correlation id as a log scope.</summary>
    [Fact]
    public async Task RequestLogEntry_CarriesTheRequestCorrelationIdScope()
    {
        var provider = new CapturingLoggerProvider();
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:RateLimit:PromptsPerMinute"] = "1" };
        using var factory = new OmpApiFactory(config, loggerProvider: provider);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var admitted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"spend the budget\"}", testToken).ConfigureAwait(true);
        admitted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var correlationId = "corr-" + Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/sessions/{id}/prompts", UriKind.Relative))
        {
            Content = new StringContent("{\"prompt\":\"over budget\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(CorrelationHeader, correlationId);
        using var rejected = await client.SendAsync(request, testToken).ConfigureAwait(true);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.GetValues(CorrelationHeader).ShouldContain(correlationId, StringComparer.Ordinal);

        var rejections = provider.Entries
            .Where(static entry => entry.Message.StartsWith("Rate limit rejected", StringComparison.Ordinal))
            .ToArray();
        rejections.ShouldNotBeEmpty("the over-budget prompt is logged as a rate-limit rejection");
        rejections.ShouldContain(entry => CarriesScope(entry, "CorrelationId", correlationId));
    }

    /// <summary>Verifies OmpOS:Debug:LogPrompts defaults to false, both on the options class and in the bound host.</summary>
    [Fact]
    public void LogPrompts_DefaultsToFalse()
    {
        new DebugOptions().LogPrompts.ShouldBeFalse();

        using var factory = new OmpApiFactory();

        factory.Services.GetRequiredService<IOptions<DebugOptions>>().Value.LogPrompts.ShouldBeFalse();
    }

    /// <summary>Verifies prompt text reaches no log entry by default, and the entry that records the prompt carries the redaction marker.</summary>
    [Fact]
    public async Task PromptText_IsNotLogged_ByDefault()
    {
        var provider = new CapturingLoggerProvider();
        using var factory = new OmpApiFactory(loggerProvider: provider);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var marker = $"PROMPTMARKER-{Guid.NewGuid():N}";
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, $"{{\"prompt\":\"Summarize {marker} please\"}}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);

        provider.Entries.Where(entry => entry.Message.Contains(marker, StringComparison.Ordinal)).ShouldBeEmpty();
        provider.Entries.ShouldContain(static entry => entry.Message.Contains("<redacted len=", StringComparison.Ordinal));
    }

    /// <summary>Verifies prompt text reaches the log when OmpOS:Debug:LogPrompts is enabled, the opt-in for verbatim prompt logging.</summary>
    [Fact]
    public async Task PromptText_IsLogged_WhenLogPromptsIsEnabled()
    {
        var provider = new CapturingLoggerProvider();
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["OmpOS:Debug:LogPrompts"] = "true" };
        using var factory = new OmpApiFactory(config, loggerProvider: provider);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var marker = $"PROMPTMARKER-{Guid.NewGuid():N}";
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, $"{{\"prompt\":\"Summarize {marker} please\"}}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);

        provider.Entries.ShouldContain(entry => entry.Message.Contains(marker, StringComparison.Ordinal), "with the opt-in, the prompt text is logged verbatim");
    }

    private static bool CarriesScope(CapturedLogEntry entry, string key, string value) =>
        entry.Scopes.Any(scope => scope is IEnumerable<KeyValuePair<string, object?>> pairs
            && pairs.Any(pair => string.Equals(pair.Key, key, StringComparison.Ordinal)
                && string.Equals(pair.Value as string, value, StringComparison.Ordinal)));
}
