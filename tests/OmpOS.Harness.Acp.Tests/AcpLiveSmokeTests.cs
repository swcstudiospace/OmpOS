using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Drives the real installed <c>omp acp</c>. Sends no prompt, so no model is contacted.</summary>
public sealed class AcpLiveSmokeTests
{
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
            HandshakeTimeout = TimeSpan.FromSeconds(60),
            RequestTimeout = TimeSpan.FromSeconds(60),
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
