using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The settings and workspace reads have a budget of their own, <c>Api:Workspace:RequestsPerMinute</c>, shared by all their routes.</summary>
public sealed class WorkspaceSettingsRateLimitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Once the budget is spent the next read, on any of the routes, answers 429 with a Retry-After hint.</summary>
    [Fact]
    public async Task BeyondTheBudget_AnyRoute_Answers429()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:RequestsPerMinute"] = "2" };
        using var factory = WorkspaceHost.Dev(null, config);
        using var client = factory.CreateClient();

        using var first = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);
        using var second = await WorkspaceHost.GetAsync(client, "/settings/network", Token).ConfigureAwait(true);
        using var third = await WorkspaceHost.GetAsync(client, "/hooks", Token).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Headers.Contains("Retry-After").ShouldBeTrue("a rate-limited read carries a Retry-After hint");
    }

    /// <summary>The reads do not draw on the prompt budget: with a budget of one prompt, a burst of settings reads still leaves the prompt endpoint open.</summary>
    [Fact]
    public async Task TheReads_DoNotSpendThePromptBudget()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Api:RateLimit:PromptsPerMinute"] = "1",
            ["Api:Workspace:RequestsPerMinute"] = "50",
        };
        using var factory = WorkspaceHost.Dev(null, config);
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);
        for (var index = 0; index < 10; index++)
        {
            using var read = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);
            read.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var prompt = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", Token).ConfigureAwait(true);

        prompt.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>A budget of zero is refused when the host starts, and the error names the key.</summary>
    [Fact]
    public void ABudgetOfZero_StopsTheHostFromStarting()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:RequestsPerMinute"] = "0" };
        using var factory = WorkspaceHost.Dev(null, config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Api:Workspace:RequestsPerMinute", Case.Sensitive);
    }
}
