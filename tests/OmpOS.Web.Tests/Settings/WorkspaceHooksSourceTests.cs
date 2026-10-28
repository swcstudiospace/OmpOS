using System.Net;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Hooks;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests.Settings;

/// <summary>The Hooks page over <c>GET /hooks</c>: a read-only list that can neither change nor run a hook.</summary>
public sealed class WorkspaceHooksSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkspaceHookInfo Hook(string id, string? command, string[]? args, bool enabled = true, bool preConsented = false) => new()
    {
        Id = id,
        PluginId = "cursor-project:abc",
        Scope = "project",
        EventName = "beforeShellExecution",
        IsOmpEvent = false,
        Type = command is null && args is null ? "prompt" : "command",
        Command = command,
        Args = args,
        Enabled = enabled,
        FailClosed = false,
        Blocking = true,
        PreConsented = preConsented,
        TimeoutSeconds = 30,
        WorkingDirectory = ".",
        SourceFile = ".cursor/hooks.json",
    };

    /// <summary>Hooks map to entries in the order the API sent them: a shell command is one argument, an argument vector keeps its elements, and a prompt hook has none.</summary>
    [Fact]
    public async Task Hooks_MapToEntries_InOrder()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new HooksResponse
        {
            Available = true,
            ConsentRecorded = false,
            Hooks = [Hook("a", "./audit.sh", null), Hook("b", null, ["prettier", "--write"], enabled: false), Hook("c", null, null), Hook("d", "./managed.sh", null, preConsented: true)],
            Sources = [],
            Diagnostics = [],
        };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.HooksResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceHooksSource(WorkspaceFixtures.Client(http));

        await source.LoadAsync(Token).ConfigureAwait(true);
        var entries = source.Snapshot();

        entries.Select(static entry => entry.Id).ShouldBe(["a", "b", "c", "d"]);
        entries[0].Arguments.ShouldBe(["./audit.sh"]);
        entries[0].Trigger.ShouldBe("beforeShellExecution");
        entries[0].PluginId.ShouldBe("cursor-project:abc");
        entries[0].WorkingDirectory.ShouldBe(".");
        entries[0].IsEnabled.ShouldBeTrue();
        entries[0].IsConsented.ShouldBeFalse();
        entries[0].Quarantine.ShouldBeNull();
        entries[1].Arguments.ShouldBe(["prettier", "--write"]);
        entries[1].IsEnabled.ShouldBeFalse();
        entries[2].Arguments.ShouldBeEmpty();
        entries[3].IsConsented.ShouldBeTrue();
    }

    /// <summary>The snapshot is empty before a load.</summary>
    [Fact]
    public void BeforeALoad_TheSnapshotIsEmpty()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var source = new WorkspaceHooksSource(WorkspaceFixtures.Client(http));

        source.Snapshot().ShouldBeEmpty();
    }

    /// <summary>A workspace the API cannot read is a load failure with the API's reason.</summary>
    [Fact]
    public async Task AnUnavailableWorkspace_FailsTheLoadWithTheReason()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new HooksResponse { Available = false, Reason = "The API has no workspace folder.", ConsentRecorded = false, Hooks = [], Sources = [], Diagnostics = [] };
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.HooksResponse);
        handler.Enqueue(queued);
        var source = new WorkspaceHooksSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<HooksSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("The API has no workspace folder.");
    }

    /// <summary>A refused call is a load failure, and a failed reload keeps the last snapshot.</summary>
    [Fact]
    public async Task AFailedReload_KeepsTheLastSnapshot()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = new HooksResponse { Available = true, ConsentRecorded = false, Hooks = [Hook("a", "./audit.sh", null)], Sources = [], Diagnostics = [] };
        using var first = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.HooksResponse);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.Unauthorized, string.Empty);
        handler.Enqueue(first);
        handler.Enqueue(refused);
        var source = new WorkspaceHooksSource(WorkspaceFixtures.Client(http));
        await source.LoadAsync(Token).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<HooksSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Sign in again", Case.Sensitive);
        source.Snapshot().ShouldHaveSingleItem().Id.ShouldBe("a");
    }

    /// <summary>Enabling, moving and granting consent are refused with the read-only sentence, and no request is sent for them.</summary>
    [Fact]
    public async Task EveryChange_IsRefused_AndSendsNothing()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var source = new WorkspaceHooksSource(WorkspaceFixtures.Client(http));

        var enable = await Should.ThrowAsync<HooksSourceException>(() => source.SetEnabledAsync("a", true, Token)).ConfigureAwait(true);
        var move = await Should.ThrowAsync<HooksSourceException>(() => source.MoveAsync("a", 0, Token)).ConfigureAwait(true);
        var consent = await Should.ThrowAsync<HooksSourceException>(() => source.GrantConsentAsync("a", Token)).ConfigureAwait(true);

        new[] { enable.Message, move.Message, consent.Message }.ShouldAllBe(static message => message == WorkspaceHooksSource.ReadOnlyMessage);
        handler.Requests.ShouldBeEmpty();
    }
}
