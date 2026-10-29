using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Connecting with an owner token: the token is checked with an authenticated request, becomes the tab's token only when the API accepts it, and is forgotten otherwise.</summary>
public sealed class OwnerTokenConnectorTests : IDisposable
{
    private const string Token = "owner-token-value-123";

    private readonly DemoTestHost _host = new();
    private readonly OwnerTokenSession _session = new();
    private readonly RoutedHandler _routes = new();
    private readonly HttpClient _http;
    private readonly OwnerTokenConnector _connector;

    /// <summary>Initializes a new instance of the <see cref="OwnerTokenConnectorTests"/> class.</summary>
    public OwnerTokenConnectorTests()
    {
        _http = new HttpClient(new BearerTokenHandler(_session, _routes, _session.Expire), disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        _connector = new OwnerTokenConnector(_session, new OmpApiClient(_http));
    }

    private async Task ScriptWorkspaceAsync()
    {
        var workspace = await _host.Api.GetWorkspaceAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        _routes.OnJson(HttpMethod.Get, "/workspace", JsonSerializer.Serialize(workspace, CatalogJsonContext.Default.WorkspaceInfo));
    }

    /// <summary>An accepted token is checked with GET /workspace carrying the bearer, signs the tab in and stays the tab's token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnAcceptedTokenSignsTheTabIn()
    {
        await ScriptWorkspaceAsync().ConfigureAwait(true);

        var outcome = await _connector.ConnectAsync(Token, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ConnectOutcome.Connected);
        _session.HasToken.ShouldBeTrue();
        _routes.Requests.ShouldBe(["GET /workspace"]);
        _routes.Headers[0]["Authorization"].ShouldBe("Bearer " + Token);
    }

    /// <summary>A refused token (401 or 403) is forgotten, the tab stays signed out and no "session ended" notice appears.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusedTokenIsForgotten(HttpStatusCode status)
    {
        _routes.OnProblem(HttpMethod.Get, "/workspace", status, null, "Refused.", "No.");

        var outcome = await _connector.ConnectAsync(Token, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ConnectOutcome.Rejected);
        _session.HasToken.ShouldBeFalse();
        _session.SessionEnded.ShouldBeFalse();
        (await _session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>An API that cannot be reached, or that fails, is not a verdict on the token: the outcome says unreachable and nothing is kept.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnreachableApiKeepsNothing()
    {
        _routes.On(HttpMethod.Get, "/workspace", _ => throw new HttpRequestException("offline"));

        var offline = await _connector.ConnectAsync(Token, TestContext.Current.CancellationToken).ConfigureAwait(true);

        offline.ShouldBe(ConnectOutcome.Unreachable);
        _session.HasToken.ShouldBeFalse();
        (await _session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();

        _routes.OnProblem(HttpMethod.Get, "/workspace", HttpStatusCode.InternalServerError, null, "Failed.", "Try later.");

        var failing = await _connector.ConnectAsync(Token, TestContext.Current.CancellationToken).ConfigureAwait(true);

        failing.ShouldBe(ConnectOutcome.Unreachable);
        _session.HasToken.ShouldBeFalse();
    }

    /// <summary>A cancelled attempt is cancelled and leaves no token behind.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ACancelledAttemptLeavesNoToken()
    {
        await ScriptWorkspaceAsync().ConfigureAwait(true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => _connector.ConnectAsync(Token, cancelled.Token)).ConfigureAwait(true);

        _session.HasToken.ShouldBeFalse();
        (await _session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _routes.Dispose();
        _host.Dispose();
    }
}
