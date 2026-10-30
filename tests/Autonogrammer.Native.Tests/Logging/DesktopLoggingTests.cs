using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Tests.Send;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Logging;

/// <summary>
/// The services the desktop app composes log through the desktop logger factory, and that output reaches the trace listener the app
/// installs at startup (ADR-0011). A Warning maps to a trace warning, and an Error to a trace error.
/// </summary>
public sealed class DesktopLoggingTests
{
    private const string Category = "Test.Category";

    /// <summary>A Warning, an Error and a Critical entry reach a trace listener as a warning, an error and an error, with the category and the exception.</summary>
    [Fact]
    public void WarningAndErrorEntriesReachATraceListenerAtTheirTraceLevels()
    {
        using var factory = DesktopLogging.CreateFactory();
        var logger = factory.CreateLogger(Category);
        using var capture = new CapturingListener();

        Emit(logger, LogLevel.Warning, "careful here", exception: null);
        Emit(logger, LogLevel.Error, "it broke", new InvalidOperationException("the cause"));
        Emit(logger, LogLevel.Critical, "it died", exception: null);

        var entries = capture.EntriesOf(Category);
        entries.Select(static entry => entry.Level).ShouldBe([TraceEventType.Warning, TraceEventType.Error, TraceEventType.Error]);
        entries[0].Message.ShouldBe($"{Category}: careful here");
        entries[1].Message.ShouldContain($"{Category}: it broke");
        entries[1].Message.ShouldContain("the cause");
        entries[2].Message.ShouldBe($"{Category}: it died");
    }

    /// <summary>An Information entry is written as a plain trace line, and a Debug entry is dropped by the factory's level.</summary>
    [Fact]
    public void InformationIsWrittenAsAPlainLineAndDebugIsDropped()
    {
        using var factory = DesktopLogging.CreateFactory();
        var logger = factory.CreateLogger(Category);
        using var capture = new CapturingListener();

        Emit(logger, LogLevel.Debug, "noise", exception: null);
        Emit(logger, LogLevel.Information, "started", exception: null);

        var entry = capture.EntriesOf(Category).ShouldHaveSingleItem();
        entry.Level.ShouldBeNull();
        entry.Message.ShouldBe($"{Category}: started");
    }

    /// <summary>The provider's logger is disabled for <see cref="LogLevel.None"/> only.</summary>
    [Fact]
    public void TheProviderLoggerIsDisabledForNoneOnly()
    {
        using var provider = new TraceLoggerProvider();
        var logger = provider.CreateLogger(Category);

        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Trace).ShouldBeTrue();
        logger.IsEnabled(LogLevel.Critical).ShouldBeTrue();
    }

    /// <summary>
    /// A pump fault of the composed ACP session is logged by the session at Error, and it reaches a trace listener as a trace error. The
    /// log call happens before the fault is delivered to the caller, so the entry is there when the failed turn surfaces.
    /// </summary>
    [Fact]
    public async Task AComposedSessionsPumpFaultReachesATraceListenerAsAnError()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        ScriptedAgentTransport? transport = null;
        using var factory = DesktopLogging.CreateFactory();
        var composed = EngineComposition.Compose(
            [],
            options => transport = new ScriptedAgentTransport(frame =>
            {
                var replies = agent.Respond(frame);
                if (Encoding.UTF8.GetString(frame).Contains("\"session/prompt\"", StringComparison.Ordinal))
                {
                    // The agent drops the connection while the prompt is in flight, so the pump ends on an open session.
                    _ = transport!.CloseAsync(CancellationToken.None);
                }

                return replies;
            }),
            factory);
        var host = composed.Host;
        await using var hostLifetime = host.ConfigureAwait(false);
        using var capture = new CapturingListener();

        await Should.ThrowAsync<HarnessException>(async () =>
        {
            await foreach (var evt in host.SubmitPromptAsync("hello", new PromptOptions(), ct).ConfigureAwait(true))
            {
                _ = evt;
            }
        }).ConfigureAwait(true);

        var entry = capture.EntriesOf("Autonogrammer.Harness.Acp.AcpAgentSession").ShouldHaveSingleItem();
        entry.Level.ShouldBe(TraceEventType.Error);
        entry.Message.ShouldContain("The omp acp pump failed for session");
    }

    private static void Emit(ILogger logger, LogLevel level, string text, Exception? exception) =>
        logger.Log(level, new EventId(1), text, exception, static (state, _) => state);

    /// <summary>One entry a trace listener received. <see cref="Level"/> is null for a plain trace line.</summary>
    /// <param name="Level">The trace level, or null for <c>Trace.WriteLine</c>.</param>
    /// <param name="Message">The message the listener received.</param>
    private sealed record TraceEntry(TraceEventType? Level, string Message);

    /// <summary>A trace listener that records what it receives for the length of its use, and removes itself when disposed.</summary>
    private sealed class CapturingListener : TraceListener
    {
        private readonly ConcurrentQueue<TraceEntry> _entries = new();

        /// <summary>Initializes a new instance of the <see cref="CapturingListener"/> class and adds it to <see cref="Trace.Listeners"/>.</summary>
        public CapturingListener() => Trace.Listeners.Add(this);

        /// <summary>Gets the entries whose message starts with <paramref name="category"/>, in the order they arrived.</summary>
        /// <param name="category">The log category the entries were written under.</param>
        /// <returns>The matching entries.</returns>
        public TraceEntry[] EntriesOf(string category) =>
            _entries.Where(entry => entry.Message.StartsWith(category, StringComparison.Ordinal)).ToArray();

        /// <inheritdoc/>
        public override void Write(string? message) => _entries.Enqueue(new TraceEntry(null, message ?? string.Empty));

        /// <inheritdoc/>
        public override void WriteLine(string? message) => _entries.Enqueue(new TraceEntry(null, message ?? string.Empty));

        /// <inheritdoc/>
        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
            _entries.Enqueue(new TraceEntry(eventType, message ?? string.Empty));

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Trace.Listeners.Remove(this);
            }

            base.Dispose(disposing);
        }
    }
}
