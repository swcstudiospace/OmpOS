using System.Net;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests.Settings;

/// <summary>The Network page over <c>GET /settings/network</c>: the upstreams the API is configured for, none of them claimed reachable.</summary>
public sealed class WorkspaceNetworkDiagnosticsSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every configured upstream is a not-checked row with the API's own sentence, stamped with the injected clock.</summary>
    [Fact]
    public async Task EveryUpstream_IsNotChecked()
    {
        var stamp = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new NetworkSettingsResponse
        {
            Checked = false,
            Reason = "The API sends no request.",
            Endpoints = [new NetworkEndpointInfo { Name = "Sign-in authority", Host = "login.example.com:8443" }, new NetworkEndpointInfo { Name = "Session store (PostgreSQL)", Host = "db.internal:5432" }],
        };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceNetworkDiagnosticsSource(WorkspaceFixtures.Client(http), new FixedWebTimeProvider(stamp));

        var report = await source.RunAsync(Token).ConfigureAwait(true);

        report.At.ShouldBe(stamp);
        report.Checks.Select(static check => (check.Name, check.Host)).ShouldBe([("Sign-in authority", "login.example.com:8443"), ("Session store (PostgreSQL)", "db.internal:5432")]);
        report.Checks.ShouldAllBe(static check => check.Status == EndpointStatus.NotChecked && check.HttpStatus == null && check.Latency == null);
        report.Checks.ShouldAllBe(static check => check.Message == "The API sends no request.");
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    /// <summary>An API with no upstream configured gives a report with no rows.</summary>
    [Fact]
    public async Task NoUpstream_IsAnEmptyReport()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, new NetworkSettingsResponse { Checked = false, Reason = "none", Endpoints = [] }, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceNetworkDiagnosticsSource(WorkspaceFixtures.Client(http), TimeProvider.System);

        var report = await source.RunAsync(Token).ConfigureAwait(true);

        report.Checks.ShouldBeEmpty();
    }

    /// <summary>A refused call is a failure of the whole run.</summary>
    [Fact]
    public async Task ARefusedCall_FailsTheRun()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.Unauthorized, string.Empty);
        handler.Enqueue(refused);
        var source = new WorkspaceNetworkDiagnosticsSource(WorkspaceFixtures.Client(http), TimeProvider.System);

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.RunAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Sign in again", Case.Sensitive);
    }
}
