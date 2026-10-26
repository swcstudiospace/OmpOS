using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>
/// A pump fault on an open session is logged at Error and surfaces through the session-failure path. Closing the
/// session on purpose ends the pump the same way, and that close is not logged as a fault.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class AcpPumpFaultLoggingTests
{
    private static readonly AcpClientOptions Options = new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    [Fact]
    public async Task PumpFaultOnAnOpenSessionIsLoggedAtErrorAndFaultsTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new CapturingLogger<AcpAgentSession>();
        ScriptedAcpTransport? transport = null;
        transport = new ScriptedAcpTransport(frame =>
        {
            if (HasMethod(frame, "session/prompt"))
            {
                // The agent drops the connection while the prompt is in flight: the pump ends with an exception.
                transport!.Disconnect();
                return [];
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = new AcpAgentSession(transport, Options, null, TimeSpan.FromSeconds(30), logger);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => DrainAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)))
            .ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
        var error = logger.Entries.Where(static entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Exception.ShouldBeOfType<DisconnectedHarnessException>();
    }

    [Fact]
    public async Task DisposingAnOpenSessionIsNotLoggedAsAPumpFault()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new CapturingLogger<AcpAgentSession>();
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, Options, null, TimeSpan.FromSeconds(30), logger);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);

        logger.Entries.Where(static entry => entry.Level == LogLevel.Error).ShouldBeEmpty();
    }

    private static bool HasMethod(byte[] frame, string method) =>
        string.Equals(AcpFixtures.MethodOf(frame), method, StringComparison.Ordinal);

    private static async Task DrainAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        await foreach (var evt in events)
        {
            _ = evt;
        }
    }
}
