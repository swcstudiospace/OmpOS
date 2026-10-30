#pragma warning disable CA2007 // Test assertions use the caller context.
using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>Disabled modules answer 404 cowork_disabled on their own. Enabling one leaves the other off. Rate-limit answers carry Retry-After.</summary>
public sealed class CoworkApiBoundaryTests
{
    /// <summary>Both modules disabled answer 404 cowork_disabled for every route.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task BothDisabled_EveryRouteAnswers404CoworkDisabled()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: false).ConfigureAwait(true);
        using var client = host.Client("alice");
        foreach (var path in new[]
        {
            "/cowork/projects",
            "/cowork/projects/p1",
            "/cowork/projects/p1/archive",
            "/cowork/instructions/global",
            "/cowork/memory",
            "/cowork/memory/recall",
            "/cowork/memory/reset",
            "/cowork/memory/export",
            "/cowork/memory/settings",
            "/cowork/memory/entry",
        })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("cowork_disabled");
        }
    }

    /// <summary>Enabling projects leaves memory disabled, and the reverse.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task EnablingProjects_LeavesMemoryDisabled()
    {
        await using var projects = await CoworkModuleHost.StartAsync(projects: true, memory: false).ConfigureAwait(true);
        using var projectClient = projects.Client("alice");
        using var listed = await projectClient.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        listed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var memoryOff = await projectClient.GetAsync(new Uri("/cowork/memory/export", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        memoryOff.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await memoryOff.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("cowork_disabled");
    }

    /// <summary>Enabling memory leaves projects disabled.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task EnablingMemory_LeavesProjectsDisabled()
    {
        await using var memory = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var memoryClient = memory.Client("alice");
        using var settings = await memoryClient.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var projectsOff = await memoryClient.GetAsync(new Uri("/cowork/instructions/global", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        projectsOff.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await projectsOff.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("cowork_disabled");
    }

    /// <summary>A rate-limited answer carries Retry-After.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task RateLimited_CarriesRetryAfter()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: true, projectsPerMinute: 1).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var first = await client.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var limited = await client.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").ShouldBeTrue();
        limited.Headers.RetryAfter?.Delta.ShouldNotBeNull();
    }
}
