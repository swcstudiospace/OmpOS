using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.AgentLoop.Sessions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>
/// A Cowork task is never a Kanban card, never a Code session and never an automation run. Cowork keeps its own file, cap and
/// <c>RecoverAfterRestart</c> pass (ADR-0032 D2, AG-69-7 and AG-69-10, decision D-32). The head is driven through its real
/// composition over a temporary tree; the Cowork registry is opened on the same state root, as the Cowork host does.
/// </summary>
public sealed class CoworkKanbanIsolationTests
{
    private const string CodeFileName = "sessions.json";
    private const string CoworkSubject = "acct-kanban";
    private static readonly string CoworkKey = CoworkAccountFolder.KeyFromSubject(CoworkSubject);

    /// <summary>A head with three Code sessions keeps exactly three cards across the create, update and close of Cowork tasks.</summary>
    [Fact]
    public async Task ACoworkTaskNeverChangesTheBoardOfThreeCodeSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var codeIds = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            codeIds.Add(await head.Host.CreateSessionAsync(ct).ConfigureAwait(true));
        }

        head.Catalog.Refresh();
        AssertBoardShows(head, codeIds);

        var cowork = OpenCowork(tree.State);
        for (var index = 0; index < 5; index++)
        {
            cowork.Create("task " + index);
        }

        cowork.List().Count.ShouldBe(5);
        head.Catalog.Refresh();
        AssertBoardShows(head, codeIds);

        var created = cowork.Create("created during the test").Id;
        head.Catalog.Refresh();
        AssertBoardShows(head, codeIds);

        cowork.SetStatus(created, CoworkSessionStatus.Running).ShouldBeTrue();
        head.Catalog.Refresh();
        AssertBoardShows(head, codeIds);

        cowork.SetStatus(created, CoworkSessionStatus.Completed).ShouldBeTrue();
        head.Catalog.Refresh();
        AssertBoardShows(head, codeIds);
        cowork.List().Count.ShouldBe(6, "the closed task stays in the Cowork list and never on the board");
    }

    /// <summary>The Code file and the Cowork file are different files, and each holds only its own tasks.</summary>
    [Fact]
    public async Task TheCoworkFileIsNotTheCodeFileAndNeitherHoldsTheOther()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var codeId = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var codePath = Path.Combine(tree.State, CodeFileName);
        File.Exists(codePath).ShouldBeTrue("the Code registry writes sessions.json at the state root");
        var codeBytes = await File.ReadAllBytesAsync(codePath, ct).ConfigureAwait(true);

        var cowork = OpenCowork(tree.State);
        var coworkId = cowork.Create("only in cowork").Id;

        string.Equals(cowork.StateFilePath, codePath, StringComparison.Ordinal).ShouldBeFalse("the Cowork file is not the Code file");
        cowork.StateFilePath.ShouldEndWith(Path.Combine("cowork", CoworkKey, CodeFileName));
        (await File.ReadAllBytesAsync(codePath, ct).ConfigureAwait(true)).ShouldBe(codeBytes, "a Cowork write must not change the Code file");
        (await File.ReadAllTextAsync(cowork.StateFilePath, ct).ConfigureAwait(true)).Contains(codeId, StringComparison.Ordinal).ShouldBeFalse();
        (await File.ReadAllTextAsync(codePath, ct).ConfigureAwait(true)).Contains(coworkId, StringComparison.Ordinal).ShouldBeFalse();
        head.Catalog.Refresh();
        head.Board.CardOf(coworkId).ShouldBeNull("a Cowork task is never a card");
    }

    /// <summary>An automation run is a Code session: it gets a card and never a row in the Cowork registry.</summary>
    [Fact]
    public async Task AnAutomationRunNeverLandsInTheCoworkRegistry()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var cowork = OpenCowork(tree.State);
        cowork.Create("first cowork task");
        cowork.Create("second cowork task");
        var coworkBytes = await File.ReadAllBytesAsync(cowork.StateFilePath, ct).ConfigureAwait(true);
        var coworkRows = cowork.List().Select(static row => row.Id).ToList();

        var runId = await head.Host.CreateAutomationSessionAsync("Nightly run", tree.Workspace, ct).ConfigureAwait(true);

        head.Catalog.Refresh();
        head.Board.CardOf(runId).ShouldNotBeNull("an automation run is a Code session and gets a card");
        cowork.List().Select(static row => row.Id).ShouldBe(coworkRows);
        (await File.ReadAllBytesAsync(cowork.StateFilePath, ct).ConfigureAwait(true)).ShouldBe(coworkBytes, "an automation run must not write the Cowork file");
    }

    /// <summary>The Cowork registry is a Cowork-only list and is not a Code catalog.</summary>
    [Fact]
    public void TheCoworkRegistryIsNotACodeCatalog()
    {
        typeof(ISessionCatalog).IsAssignableFrom(typeof(CoworkSessionRegistry)).ShouldBeFalse();
        typeof(ICoworkSessionCatalog).IsAssignableFrom(typeof(AgentSessionRegistry)).ShouldBeFalse();
    }

    /// <summary>A full Code registry refuses a Code task and leaves the Cowork registry writable.</summary>
    [Fact]
    public void AFullCodeRegistryLeavesCoworkWritable()
    {
        using var tree = new TempTree();
        var code = OpenCode(Path.Combine(tree.State, CodeFileName), tree.State);
        FillCode(code);
        Should.Throw<AgentSessionLimitException>(() => code.Create("one more", null));

        var cowork = OpenCowork(tree.State);
        cowork.Create("still writable").Status.ShouldBe(CoworkSessionStatus.Created);
        cowork.List().Count.ShouldBe(1);
        code.Snapshot().Count.ShouldBe(AgentSessionRegistry.MaxSessions);
    }

    /// <summary>A full Cowork registry refuses a Cowork task and leaves the Code registry writable.</summary>
    [Fact]
    public void AFullCoworkRegistryLeavesCodeWritable()
    {
        using var tree = new TempTree();
        var cowork = OpenCowork(tree.State);
        for (var index = 0; index < AgentSessionRegistry.MaxSessions; index++)
        {
            cowork.Create("task " + index);
        }

        Should.Throw<CoworkSessionLimitException>(() => cowork.Create("one more"));

        var code = OpenCode(Path.Combine(tree.State, CodeFileName), tree.State);
        code.Create("still writable", null).Lifecycle.ShouldBe(SessionLifecycle.Created);
        code.Snapshot().Count.ShouldBe(1);
        cowork.List().Count.ShouldBe(AgentSessionRegistry.MaxSessions);
    }

    /// <summary>A Crash-then-Recover pass on the Code side touches only Code rows, and the Cowork pass touches only Cowork rows.</summary>
    [Fact]
    public async Task EachRecoverAfterRestartPassTouchesOnlyItsOwnRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var codePath = Path.Combine(tree.State, CodeFileName);
        var code = OpenCode(codePath, tree.State);
        var codeRunning = code.Create("code running", null).Id;
        code.SetLifecycle(codeRunning, SessionLifecycle.Running).ShouldBeTrue();
        var codeCreated = code.Create("code created", null).Id;

        var cowork = OpenCowork(tree.State);
        var coworkRunning = cowork.Create("cowork running").Id;
        var coworkPaused = cowork.Create("cowork paused").Id;
        cowork.SetStatus(coworkRunning, CoworkSessionStatus.Running).ShouldBeTrue();
        cowork.SetStatus(coworkPaused, CoworkSessionStatus.Paused).ShouldBeTrue();
        var coworkBytes = await File.ReadAllBytesAsync(cowork.StateFilePath, ct).ConfigureAwait(true);

        code.RecoverAfterRestart().ShouldBe([codeRunning]);
        code.TryGet(codeRunning, out var lostCode).ShouldBeTrue();
        lostCode!.Lifecycle.ShouldBe(SessionLifecycle.Lost);
        code.TryGet(codeCreated, out var untouchedCode).ShouldBeTrue();
        untouchedCode!.Lifecycle.ShouldBe(SessionLifecycle.Created);
        (await File.ReadAllBytesAsync(cowork.StateFilePath, ct).ConfigureAwait(true)).ShouldBe(coworkBytes, "the Code pass must not write the Cowork file");
        StatusOf(cowork, coworkRunning).ShouldBe(CoworkSessionStatus.Running);
        StatusOf(cowork, coworkPaused).ShouldBe(CoworkSessionStatus.Paused);

        var codeAfterCodePass = await File.ReadAllBytesAsync(codePath, ct).ConfigureAwait(true);
        cowork.RecoverAfterRestart().ShouldBe([coworkRunning]);
        StatusOf(cowork, coworkRunning).ShouldBe(CoworkSessionStatus.Lost);
        StatusOf(cowork, coworkPaused).ShouldBe(CoworkSessionStatus.Paused);
        (await File.ReadAllBytesAsync(codePath, ct).ConfigureAwait(true)).ShouldBe(codeAfterCodePass, "the Cowork pass must not write the Code file");
        code.TryGet(codeRunning, out var stillLost).ShouldBeTrue();
        stillLost!.Lifecycle.ShouldBe(SessionLifecycle.Lost);
    }

    private static CoworkSessionStatus StatusOf(CoworkSessionRegistry cowork, string id) =>
        cowork.List().Single(row => string.Equals(row.Id, id, StringComparison.Ordinal)).Status;

    private static void AssertBoardShows(AgentHead head, List<string> codeIds)
    {
        head.Board.Cards.Count.ShouldBe(codeIds.Count, "the board shows only the Code sessions");
        head.Board.Cards.Select(static card => card.Id).ShouldBe(codeIds, ignoreOrder: true);
    }

    private static void FillCode(AgentSessionRegistry code)
    {
        for (var index = 0; index < AgentSessionRegistry.MaxSessions; index++)
        {
            code.Create("code " + index, null);
        }
    }

    private static AgentSessionRegistry OpenCode(string path, string stateRoot) =>
        new(path, TimeProvider.System, options: new AgentSessionRegistryOptions { StateRoot = stateRoot });

    private static CoworkSessionRegistry OpenCowork(string stateRoot) =>
        CoworkSessionRegistry.Open(new CoworkSessionOptions { StateRoot = stateRoot }, new FixedGate(CoworkKey), TimeProvider.System);

    private sealed class FixedGate(string key) : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => key;
    }
}
