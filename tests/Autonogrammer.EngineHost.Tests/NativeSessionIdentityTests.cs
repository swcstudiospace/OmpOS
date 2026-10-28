using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.EngineHost.Tests;

/// <summary>
/// The native session identifier of <see cref="InProcessEngineHost"/>: it is reported once the bound session is created or resumed,
/// and a host built with a resume identifier opens its first session with <c>ResumeAsync</c> instead of <c>CreateAsync</c>.
/// Every session is scripted, so no test spawns omp.
/// </summary>
public sealed class NativeSessionIdentityTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    /// <summary>The identifier is null before a prompt, is published once when the session is created, and is not published again by the next prompt.</summary>
    [Fact]
    public async Task TheIdentifierIsPublishedOnceWhenTheSessionIsCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<ScriptedSession>();
        var host = new InProcessEngineHost(() => Track(sessions, new ScriptedSession("native-1")), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        var raised = 0;
        host.NativeSessionIdChanged += (_, _) => raised++;
        host.NativeSessionId.ShouldBeNull();

        await DrainAsync(host, "first", ct).ConfigureAwait(true);
        await DrainAsync(host, "second", ct).ConfigureAwait(true);

        host.NativeSessionId.ShouldBe("native-1");
        raised.ShouldBe(1);
        sessions.Count.ShouldBe(1);
        sessions[0].Created.ShouldBeTrue();
        sessions[0].ResumedWith.ShouldBeNull();
    }

    /// <summary>The identifier is already readable inside the handler of the change event.</summary>
    [Fact]
    public async Task TheIdentifierIsReadableInsideTheChangeHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new ScriptedSession("native-7"), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        string? seen = null;
        host.NativeSessionIdChanged += (sender, _) => seen = ((InProcessEngineHost)sender!).NativeSessionId;

        await DrainAsync(host, "go", ct).ConfigureAwait(true);

        seen.ShouldBe("native-7");
    }

    /// <summary>A host built with a resume identifier resumes its first session, reports the resumed identifier, and never calls CreateAsync.</summary>
    [Fact]
    public async Task AHostWithAResumeIdentifierResumesInsteadOfCreating()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<ScriptedSession>();
        var host = new InProcessEngineHost(() => Track(sessions, new ScriptedSession("ignored-until-resumed")), Descriptor, "stored-native-id");
        await using var hostScope = host.ConfigureAwait(true);

        await DrainAsync(host, "continue", ct).ConfigureAwait(true);

        sessions.Count.ShouldBe(1);
        sessions[0].ResumedWith.ShouldBe("stored-native-id");
        sessions[0].Created.ShouldBeFalse();
        host.NativeSessionId.ShouldBe("stored-native-id");
    }

    /// <summary>The resume applies to the first session only: a session bound after the first one faulted is created fresh.</summary>
    [Fact]
    public async Task AFaultedSessionIsReplacedByAFreshOneNotAnotherResume()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<ScriptedSession>();
        var host = new InProcessEngineHost(() => Track(sessions, new ScriptedSession($"native-{sessions.Count + 1}")), Descriptor, "stored-native-id");
        await using var hostScope = host.ConfigureAwait(true);
        await DrainAsync(host, "one", ct).ConfigureAwait(true);
        sessions[0].State = HarnessState.Faulted;

        await DrainAsync(host, "two", ct).ConfigureAwait(true);

        sessions.Count.ShouldBe(2);
        sessions[1].Created.ShouldBeTrue();
        sessions[1].ResumedWith.ShouldBeNull();
        host.NativeSessionId.ShouldBe("native-2");
    }

    /// <summary>A resume that fails disposes the session, surfaces the failure, and is attempted again by the next prompt rather than silently starting a new conversation.</summary>
    [Fact]
    public async Task AFailedResumeIsNotReplacedByANewConversation()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<ScriptedSession>();
        var host = new InProcessEngineHost(
            () => Track(sessions, new ScriptedSession("native-x") { FailResume = sessions.Count == 0 }),
            Descriptor,
            "stored-native-id");
        await using var hostScope = host.ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => DrainAsync(host, "one", ct)).ConfigureAwait(true);
        sessions[0].Disposed.ShouldBeTrue();
        sessions[0].Created.ShouldBeFalse();
        host.NativeSessionId.ShouldBeNull();

        await DrainAsync(host, "two", ct).ConfigureAwait(true);

        sessions[1].ResumedWith.ShouldBe("stored-native-id");
        sessions[1].Created.ShouldBeFalse();
    }

    /// <summary>A session that does not name itself leaves the identifier null and never raises the event.</summary>
    [Fact]
    public async Task ASessionWithoutAnIdentityReportsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new AnonymousSession(), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        var raised = 0;
        host.NativeSessionIdChanged += (_, _) => raised++;

        await DrainAsync(host, "go", ct).ConfigureAwait(true);

        host.NativeSessionId.ShouldBeNull();
        raised.ShouldBe(0);
    }

    /// <summary>A handler that throws neither fails the prompt nor hides the identifier from the handlers after it.</summary>
    [Fact]
    public async Task AThrowingHandlerDoesNotFailThePromptOrHideTheIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new ScriptedSession("native-9"), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        string? seen = null;
        host.NativeSessionIdChanged += (_, _) => throw new InvalidOperationException("subscriber fault");
        host.NativeSessionIdChanged += (sender, _) => seen = ((InProcessEngineHost)sender!).NativeSessionId;

        var events = await DrainAsync(host, "go", ct).ConfigureAwait(true);

        events.ShouldHaveSingleItem().ShouldBeOfType<CompletedEvent>();
        seen.ShouldBe("native-9");
    }

    private static ScriptedSession Track(List<ScriptedSession> sessions, ScriptedSession session)
    {
        sessions.Add(session);
        return session;
    }

    private static async Task<List<HarnessEvent>> DrainAsync(InProcessEngineHost host, string prompt, CancellationToken cancellationToken)
    {
        var events = new List<HarnessEvent>();
        await foreach (var evt in host.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        return events;
    }

    /// <summary>A session that names itself when it is created or resumed and ends every turn with a completion.</summary>
    private sealed class ScriptedSession(string createdId) : IOmpSession, IOmpSessionIdentity
    {
        public string? NativeSessionId { get; private set; }

        public bool Created { get; private set; }

        public string? ResumedWith { get; private set; }

        public bool Disposed { get; private set; }

        public bool FailResume { get; init; }

        public HarnessState State { get; set; } = HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken)
        {
            Created = true;
            NativeSessionId = createdId;
            return Task.CompletedTask;
        }

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (FailResume)
            {
                throw new InvalidOperationException("The engine could not load the session.");
            }

            ResumedWith = sessionId;
            NativeSessionId = sessionId;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A session that does not implement <see cref="IOmpSessionIdentity"/>.</summary>
    private sealed class AnonymousSession : IOmpSession
    {
        public HarnessState State => HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
