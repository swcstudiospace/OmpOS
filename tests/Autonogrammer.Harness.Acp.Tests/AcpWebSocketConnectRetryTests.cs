using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>A connect attempt that fails before a socket opens does not leave the transport stuck: the next attempt runs and reaches the worker.</summary>
public sealed class AcpWebSocketConnectRetryTests
{
    private const string Token = "worker-jwt-value";

    /// <summary>After a token failure, a second connect runs its own attempt and the worker accepts it with the bearer token.</summary>
    [Fact]
    public async Task AFailedConnectAttemptDoesNotBlockTheNextOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = new TestWebSocketServer();
        var attempts = 0;
        var transport = new AcpWebSocketTransport(new AcpWebSocketTransportOptions
        {
            Endpoint = server.Endpoint,
            BearerTokenProvider = _ =>
            {
                attempts++;
                return attempts == 1 ? throw new InvalidOperationException("token store unavailable") : ValueTask.FromResult(Token);
            },
        });
        await using var transportScope = transport.ConfigureAwait(true);

        await Should.ThrowAsync<ProcessStartHarnessException>(() => transport.ConnectAsync(ct)).ConfigureAwait(true);

        var accepting = server.AcceptAsync(ct);
        await transport.ConnectAsync(ct).ConfigureAwait(true);
        using var session = await accepting.ConfigureAwait(true);

        session.Authorization.ShouldBe("Bearer " + Token);
        attempts.ShouldBe(2);
    }
}
