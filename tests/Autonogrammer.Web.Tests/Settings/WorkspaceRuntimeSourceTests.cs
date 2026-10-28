using System.Net;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>The read-only Runtime values over <c>GET /settings/runtime</c>.</summary>
public sealed class WorkspaceRuntimeSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The values reach the page unchanged.</summary>
    [Fact]
    public async Task TheValues_ReachThePageUnchanged()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new RuntimeSettingsResponse
        {
            HarnessAdapter = "Acp",
            SessionStore = "Postgres",
            CoordinationStore = "Dragonfly",
            RunLeaseSeconds = 30,
            RunLeaseRenewSeconds = 10,
            MaxPromptLength = 64000,
            MaxRequestBodyBytes = 1_048_576,
            PromptsPerMinute = 60,
            SessionsPerMinute = 40,
            WorkspaceRequestsPerMinute = 120,
            WorkspaceConfigured = true,
            IncludesUserScope = false,
            MaxWorkspaceFileBytes = 1_048_576,
        };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceRuntimeSource(WorkspaceFixtures.Client(http));

        var values = await source.LoadAsync(Token).ConfigureAwait(true);

        values.ShouldBe(answer);
        handler.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe("/settings/runtime");
    }

    /// <summary>A refused call is the page's error state.</summary>
    [Fact]
    public async Task ARefusedCall_IsTheErrorState()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.Unauthorized, string.Empty);
        handler.Enqueue(refused);
        var source = new WorkspaceRuntimeSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Sign in again", Case.Sensitive);
    }
}
