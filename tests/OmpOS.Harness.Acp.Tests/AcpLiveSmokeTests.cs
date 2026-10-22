using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Drives the real installed <c>omp acp</c>. Sends no prompt, so no model is contacted.</summary>
public sealed class AcpLiveSmokeTests
{
    /// <summary>
    /// Bound for the live handshake and for each request (<c>initialize</c>, <c>authenticate</c>, <c>session/new</c>).
    /// Measured spawn to <c>session/new</c> result on the gate host: 17.6, 18.0 and 43.0 s alone (host load 13 to 20,
    /// omp using 15.5 to 19.4 CPU-seconds); 66.2, 67.7 and 97.0 s under a bounded 8x <c>yes</c> load (host load 21 to 54).
    /// <c>initialize</c> took up to 56.2 s under that load, and the <c>session/new</c> call itself up to 44.5 s. A 60 s bound
    /// leaves too little margin, and the gate timed out at it. 300 s is about three times the worst measured spawn to <c>session/new</c> (97 s), and still fails a real hang.
    /// </summary>
    private static readonly TimeSpan LiveAgentTimeout = TimeSpan.FromSeconds(300);

    /// <summary>Initialize, session/new and session/close against the installed omp; skipped when omp is not on PATH.</summary>
    [Fact]
    public async Task InstalledOmpAnswersInitializeSessionNewAndClose()
    {
        if (!IsOnPath("omp"))
        {
            Assert.Skip("omp is not on PATH; the live ACP smoke test is skipped.");
        }

        var ct = TestContext.Current.CancellationToken;
        var options = new AcpClientOptions
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            AuthMethodId = "agent",
            HandshakeTimeout = LiveAgentTimeout,
            RequestTimeout = LiveAgentTimeout,
        };
        var session = (AcpAgentSession)new OmpAcpClient().CreateSession(options);
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            session.AgentInfo.ShouldNotBeNull();
            session.AgentInfo.Name.ShouldBe("omp");
            session.SessionId.ShouldNotBeNullOrWhiteSpace();
            session.AdvertisedCapabilities["loadSession"].ShouldBe("true");
            session.AdvertisedCapabilities["mcpCapabilities.http"].ShouldBe("true");
            session.AdvertisedCapabilities["promptCapabilities.image"].ShouldBe("true");
            session.AdvertisedCapabilities["sessionCapabilities.close"].ShouldBe("true");
            session.State.ShouldBe(HarnessState.Ready);

            await session.CloseSessionAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static bool IsOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, executable)));
}
