using System.Net;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Terminal;
using OmpOS.Web.Demo;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The web implementations of the Presentation host and source interfaces map API answers to the Presentation records, and unavailable features surface a reason instead of throwing.</summary>
public sealed class WebSourcesTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>The tool approval bridge maps every answer of the decision route to an outcome.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="expected">The outcome.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(204, ToolApprovalOutcome.Accepted)]
    [InlineData(404, ToolApprovalOutcome.NotFound)]
    [InlineData(409, ToolApprovalOutcome.AlreadyDecided)]
    [InlineData(403, ToolApprovalOutcome.Refused)]
    [InlineData(500, ToolApprovalOutcome.Failed)]
    public async Task ApprovalBridgeMapsEveryAnswer(int status, ToolApprovalOutcome expected)
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Post, "/sessions/s-1/tool-approvals/r-1", _ => new HttpResponseMessage((HttpStatusCode)status));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        var bridge = new WebToolApprovalBridge(new OmpApiClient(http));
        string? decidedFor = null;
        bridge.Decided += (_, decided) => decidedFor = decided.SessionId;

        var outcome = await bridge.DecideAsync("s-1", "r-1", ToolApprovalDecision.Allow, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(expected);
        handler.Bodies.Single().ShouldBe("""{"decision":"allow"}""");
        if (status != 500)
        {
            decidedFor.ShouldBe("s-1");
        }
    }

    /// <summary>An unreachable API is an outcome, not an exception.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ApprovalBridgeReportsAnUnreachableApiAsFailed()
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Post, "/sessions/s-1/tool-approvals/r-1", _ => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var outcome = await new WebToolApprovalBridge(new OmpApiClient(http)).DecideAsync("s-1", "r-1", ToolApprovalDecision.Deny, TestContext.Current.CancellationToken).ConfigureAwait(true);

        outcome.ShouldBe(ToolApprovalOutcome.Failed);
        handler.Bodies.Single().ShouldBe("""{"decision":"deny"}""");
    }

    /// <summary>A steer from a Kanban card is delivered, queued, or refused with the API's reason.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CardSteeringMapsDeliveryAndRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        var steering = new WebCardSteering(_host.Api);

        var refused = await steering.SteerAsync(DemoFixtures.CompletedId, "go on", CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var held = await steering.SteerAsync(DemoFixtures.ApprovalId, "wait", CardSteerMode.SendNow, ct).ConfigureAwait(true);
        var queued = await steering.SteerAsync(DemoFixtures.ApprovalId, "later", CardSteerMode.FollowUp, ct).ConfigureAwait(true);

        refused.Outcome.ShouldBe(CardSteerOutcome.Failed);
        refused.Message.ShouldContain("No turn is running");
        held.Outcome.ShouldBe(CardSteerOutcome.Queued);
        held.Message.ShouldContain("Held");
        queued.Outcome.ShouldBe(CardSteerOutcome.Queued);
    }

    /// <summary>The card details come from the API; a session without a task reports unavailable.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task KanbanDetailsMapTheApiAnswer()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new WebKanbanDetailsSource(_host.Api);

        var bound = await source.LoadAsync(DemoFixtures.RunningId, ct).ConfigureAwait(true);
        var unbound = await source.LoadAsync(DemoFixtures.TerminalId, ct).ConfigureAwait(true);

        bound.ShouldNotBeNull().Available.ShouldBeTrue();
        bound.TaskTitle.ShouldBe("Fix the checkout total rounding");
        bound.LessonsCount.ShouldBe(1);
        bound.LeaseHolder.ShouldBe("demo-agent-1");
        unbound.ShouldNotBeNull().Available.ShouldBeFalse();
    }

    /// <summary>The task context loads for a bound session and shows "unavailable" with the API's reason for an unbound one.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TaskContextShowsTheReasonWhenUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new WebTaskContextSource(_host.Api);
        using var bound = new TaskContextModel(source, TimeProvider.System);
        using var unbound = new TaskContextModel(source, TimeProvider.System);

        await bound.SelectAsync(Scope(DemoFixtures.RunningId)).ConfigureAwait(true);
        await unbound.SelectAsync(Scope(DemoFixtures.TerminalId)).ConfigureAwait(true);

        bound.State.ShouldBe(TaskContextState.Ready);
        bound.Title.ShouldBe("Fix the checkout total rounding");
        unbound.State.ShouldBe(TaskContextState.Unavailable);
        unbound.Notice.ShouldBe(TaskContextNotice.Failed);
        unbound.Body.ShouldBe("No task is bound to this Demo session.");
        _ = ct;
    }

    /// <summary>The stage source yields the stages the API reports, then every stage published to it, and stops with the token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task StageSourceYieldsTheSnapshotThenPublishedStages()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var source = new WebStageSource(_host.Api, DemoFixtures.RunningId);
        var seen = new List<StageEvent>();
        var published = new StageEvent { SessionId = DemoFixtures.RunningId, Source = StageSource.Swarm, StageId = "swarm/new", Title = "Reviewer", Status = StageStatus.Running, Timestamp = DateTimeOffset.UnixEpoch };

        try
        {
            await foreach (var stage in source.ObserveAsync(Scope(DemoFixtures.RunningId), cancellation.Token).ConfigureAwait(true))
            {
                seen.Add(stage);
                if (seen.Count == 7)
                {
                    source.Publish(published);
                }

                if (seen.Count == 8)
                {
                    await cancellation.CancelAsync().ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The test cancels the observation after the published stage.
        }

        seen.Count.ShouldBe(8);
        seen[0].StageId.ShouldBe("omp/turn");
        seen[7].StageId.ShouldBe("swarm/new");
    }

    /// <summary>The web terminal transport says why there is no terminal, as the terminal's output, then ends.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UnavailableTerminalShowsItsReasonAsOutput()
    {
        var model = new TerminalSurfaceModel(TerminalHostKind.Web, new UnavailableTerminalTransport());

        await model.AttachRemoteAsync("t-1", TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.Output.ShouldContain("not available in the web head");
        model.State.ShouldBe(RemoteTerminalState.ConnectionLost);
        model.LocalAvailability.IsAvailable.ShouldBeFalse();
    }

    /// <summary>The Demo terminal resumes after the acknowledged sequence and stays attached until cancelled.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DemoTerminalResumesAfterTheAcknowledgedSequence()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var transport = new DemoTerminalTransport(_host.Time, TimeSpan.Zero);
        var chunks = new List<RemoteTerminalChunk>();

        try
        {
            await foreach (var chunk in transport.AttachAsync("t-1", 10, cancellation.Token).ConfigureAwait(true))
            {
                chunks.Add(chunk);
                if (chunks.Count == 2)
                {
                    await cancellation.CancelAsync().ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // After the last chunk the attachment stays open until the view detaches: the cancellation above ends it.
        }

        chunks.Select(static chunk => chunk.Sequence).ShouldBe(new long[] { 11, 12 });
        chunks[0].Text.ShouldNotBeNullOrEmpty();
    }

    /// <summary>The Changes mirror holds the agent's text, refuses a stale write and a missing file, and is never the local file system.</summary>
    [Fact]
    public void WorkspaceMirrorGuardsWritesWithTheExpectedText()
    {
        var files = new WebWorkspaceFileSystem();
        files.Reset([new KeyValuePair<string, string>("a.txt", "new")]);

        files.RootPath.ShouldBe(WebWorkspaceFileSystem.VirtualRoot);
        files.ReadFile("a.txt").ShouldBe("new");
        Should.Throw<StaleWorkspaceFileException>(() => files.WriteFile("a.txt", "old", "something else"));
        files.WriteFile("a.txt", "old", "new");
        files.ReadFile("a.txt").ShouldBe("old");
        files.DeleteFile("a.txt", "old");
        Should.Throw<FileNotFoundException>(() => files.ReadFile("a.txt"));
        files.CreateFile("b.txt", "x");
        files.ReadFile("b.txt").ShouldBe("x");
        files.ListDirectory(string.Empty).ShouldBeEmpty();
    }

    /// <summary>The slash action host runs the actions the browser can reach and reports the rest as not available.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SlashHostRunsReachableActionsAndRefusesTheRest()
    {
        var ct = TestContext.Current.CancellationToken;
        var opened = new List<string>();
        var host = new WebSlashActionHost(new WebSlashTargets
        {
            OpenSettings = name =>
            {
                opened.Add(name);
                return name.Length > 0;
            },
            SetMode = (mode, _) => Task.FromResult<string?>(string.Equals(mode, "plan", StringComparison.Ordinal) ? null : "No such mode."),
            PickModel = (filter, _) => Task.FromResult("Model: " + filter),
        });

        host.CanRun(OmpOS.Presentation.Commands.SlashActionIds.SettingsOpen).ShouldBeTrue();
        host.CanRun(OmpOS.Presentation.Commands.SlashActionIds.ChatNew).ShouldBeFalse("no target was given");
        host.CanRun(OmpOS.Presentation.Commands.SlashActionIds.TerminalNew).ShouldBeFalse();

        var settings = await host.RunAsync(new OmpOS.Presentation.Commands.SlashActionRequest(OmpOS.Presentation.Commands.SlashActionIds.SettingsOpen, "models", "/settings models"), ct).ConfigureAwait(true);
        var missing = await host.RunAsync(new OmpOS.Presentation.Commands.SlashActionRequest(OmpOS.Presentation.Commands.SlashActionIds.SettingsOpen, string.Empty, "/settings"), ct).ConfigureAwait(true);
        var mode = await host.RunAsync(new OmpOS.Presentation.Commands.SlashActionRequest(OmpOS.Presentation.Commands.SlashActionIds.ModeSet, "plan", "/plan fix it"), ct).ConfigureAwait(true);
        var badMode = await host.RunAsync(new OmpOS.Presentation.Commands.SlashActionRequest(OmpOS.Presentation.Commands.SlashActionIds.ModeSet, "turbo", "/turbo"), ct).ConfigureAwait(true);
        var terminal = await host.RunAsync(new OmpOS.Presentation.Commands.SlashActionRequest(OmpOS.Presentation.Commands.SlashActionIds.TerminalNew, string.Empty, "/terminal"), ct).ConfigureAwait(true);

        settings.Outcome.ShouldBe(OmpOS.Presentation.Commands.SlashActionOutcome.Completed);
        missing.Outcome.ShouldBe(OmpOS.Presentation.Commands.SlashActionOutcome.Failed);
        mode.FollowUpPrompt.ShouldBe("/plan fix it");
        badMode.Outcome.ShouldBe(OmpOS.Presentation.Commands.SlashActionOutcome.Failed);
        terminal.Outcome.ShouldBe(OmpOS.Presentation.Commands.SlashActionOutcome.NotAvailable);
        terminal.Message.ShouldBe(WebSlashActionHost.NotInWebHead);
        opened.ShouldBe(new[] { "models", string.Empty });
    }

    /// <summary>The panel host cannot create an omp terminal in the browser and says why; creating a chat adds it to the catalogue.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PanelHostRefusesATerminalAndCreatesAChat()
    {
        var ct = TestContext.Current.CancellationToken;
        var catalog = new WebSessionCatalog();
        var host = new WebAgentPanelHost(_host.Api, catalog, _ => throw new InvalidOperationException("not opened here"), _ => Task.CompletedTask);

        var terminal = await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTerminalSessionAsync(ct)).ConfigureAwait(true);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);

        terminal.Message.ShouldBe(TerminalAvailability.WebReason);
        catalog.Find(id).ShouldNotBeNull().RowStatus.ShouldBe(OmpOS.Contracts.Catalog.CatalogRowStatus.Ready);
        catalog.Entries.Count.ShouldBe(7);
        await host.CloseSessionAsync(id, ct).ConfigureAwait(true);
        catalog.IsClosed(id).ShouldBeTrue();
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private static StageObservationScope Scope(string id) => new() { SessionId = id, WorkspaceRoot = "web", SessionStateDirectory = "web" };
}
