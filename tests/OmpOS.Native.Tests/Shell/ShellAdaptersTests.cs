using System.Text.Json;
using OmpOS.Contracts.Streaming;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Views.Shell;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.State;
using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shell;

/// <summary>The small adapters between the slash handlers and the shell: what the copy commands read from a session, and the saved workspace sets.</summary>
public sealed class ShellAdaptersTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The copy commands read the session id, the request id the engine reported, the last response and the last message the user sent.</summary>
    [Fact]
    public async Task TheSessionContextReadsTheTranscript()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var runtime = fixture.Runtime("session-1");
            string? requestId = null;
            var context = new SessionSlashContext(runtime.ViewModel, () => requestId);

            context.SessionId.ShouldBe("session-1", Case.Sensitive);
            context.LastRequestId.ShouldBeNull();
            context.LastResponseText.ShouldBeNull();
            context.LastUserMessageText.ShouldBeNull();

            Apply(runtime.Session, 1, new UnknownPayload { OriginalKind = "prompt_accepted", Raw = Json("""{"kind":"prompt_accepted","prompt":"explain the build"}""") });
            Apply(runtime.Session, 2, new TextDeltaPayload { Delta = "The build has two steps." });
            Apply(runtime.Session, 3, new CompletedPayload { Reason = "end_turn" });
            requestId = "request-7";

            context.LastUserMessageText.ShouldBe("explain the build", Case.Sensitive);
            context.LastResponseText.ShouldBe("The build has two steps.", Case.Sensitive);
            context.LastRequestId.ShouldBe("request-7", Case.Sensitive);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Saving records the current folders under the name; loading finds the set by name, ignoring case, and hands its folders to the starter with the first as primary.</summary>
    [Fact]
    public async Task WorkspaceSetsSaveAndLoadByName()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ompos-sets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var store = new WorkspaceSetStore(Path.Combine(directory, "sets.json"));
            IReadOnlyList<WorkspacePickerRoot>? started = null;
            var backend = new WorkspaceSetsBackend(
                store,
                () => ["/work/app", "/work/lib"],
                (roots, _) =>
                {
                    started = roots;
                    return Task.FromResult(true);
                });

            await backend.SaveAsync("Demo", Token).ConfigureAwait(true);

            (await backend.ListAsync(Token).ConfigureAwait(true)).ShouldBe(["Demo"]);
            (await backend.LoadAsync("demo", Token).ConfigureAwait(true)).ShouldBeTrue();
            var roots = started.ShouldNotBeNull();
            roots.Select(static root => root.Path).ShouldBe(["/work/app", "/work/lib"]);
            roots[0].IsPrimary.ShouldBeTrue();
            roots[1].IsPrimary.ShouldBeFalse();

            started = null;
            (await backend.LoadAsync("missing", Token).ConfigureAwait(true)).ShouldBeFalse();
            started.ShouldBeNull("an unknown name starts nothing");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A set with no folder cannot be saved, and a blank name is refused.</summary>
    [Fact]
    public async Task WorkspaceSetsRefuseNoFoldersAndBlankNames()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ompos-sets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var store = new WorkspaceSetStore(Path.Combine(directory, "sets.json"));
            var empty = new WorkspaceSetsBackend(store, () => [], (_, _) => Task.FromResult(true));
            var backend = new WorkspaceSetsBackend(store, () => ["/work/app"], (_, _) => Task.FromResult(true));

            await Should.ThrowAsync<InvalidOperationException>(() => empty.SaveAsync("demo", Token)).ConfigureAwait(true);
            await Should.ThrowAsync<ArgumentException>(() => backend.SaveAsync(" ", Token)).ConfigureAwait(true);
            (await backend.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static void Apply(SessionViewModel session, long sequence, StreamEventPayload payload) => session.ApplyEvent(new StreamEvent
    {
        Seq = sequence,
        SessionId = "session-1",
        CorrelationId = "session-1/turn-1",
        Timestamp = Stamp,
        Payload = payload,
    });
}
