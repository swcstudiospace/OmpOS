using System.Net;
using System.Net.Http.Headers;
using Autonogrammer.Contracts.Runs;
using Autonogrammer.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Hosted run authorisation on a Development host with the development principal off. Run operations follow the session endpoints: they
/// need a bearer token (401), and another subject's session or run reads as absent (404), so run identifiers are not revealed.
/// </summary>
public sealed class CloudRunOwnerScopeTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Another subject's create, status, output and cancel calls answer 404 and change nothing; the owner's calls succeed.</summary>
    [Fact]
    public async Task AnotherPrincipalsRunOperationsAnswer404AndChangeNothing()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var alice = AsPrincipal(factory, "alice");
        using var bob = AsPrincipal(factory, "bob");
        var sessionId = await SessionClient.CreateSessionAsync(alice, Token).ConfigureAwait(true);
        var run = await CloudRunClient.CreateRunAsync(alice, sessionId, "mine", Token).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(alice, sessionId, SessionStatus.Completed, SettleTimeout, Token).ConfigureAwait(true);

        foreach (var (method, path) in CloudRunTests.RunOperations(sessionId, run.Id))
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
            using var response = await bob.SendAsync(request, Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }

        using var refusedCreate = await CloudRunClient.PostRunAsync(bob, sessionId, "intrude", Token).ConfigureAwait(true);
        refusedCreate.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var untouched = await SessionClient.ReadSessionAsync(alice, sessionId, Token).ConfigureAwait(true);
        untouched.Messages.ShouldHaveSingleItem().Content.ShouldBe("mine");
        (await CloudRunClient.GetRunAsync(alice, sessionId, run.Id, Token).ConfigureAwait(true)).Status.ShouldBe(RunStatus.Completed);
    }

    /// <summary>Without a bearer token every run operation answers 401, and a refused create admits no prompt.</summary>
    [Fact]
    public async Task AnonymousRunOperationsAnswer401AndAdmitNothing()
    {
        using var factory = JwtTestHost.CreateFactory();
        using var anonymous = factory.CreateClient();
        using var alice = AsPrincipal(factory, "alice");
        var sessionId = await SessionClient.CreateSessionAsync(alice, Token).ConfigureAwait(true);

        using var create = await CloudRunClient.PostRunAsync(anonymous, sessionId, "intrude", Token).ConfigureAwait(true);
        create.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        foreach (var (method, path) in CloudRunTests.RunOperations(sessionId, "any-run"))
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
            using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path);
        }

        var untouched = await SessionClient.ReadSessionAsync(alice, sessionId, Token).ConfigureAwait(true);
        untouched.Status.ShouldBe(SessionStatus.Idle);
        untouched.Messages.ShouldBeEmpty();
    }

    private static HttpClient AsPrincipal(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(subject));
        return client;
    }
}
