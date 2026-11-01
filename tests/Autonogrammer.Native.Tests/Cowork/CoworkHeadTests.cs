using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Automations;
using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>Accessible names, focus order, reduced motion, conversation selection and owned Cowork shutdown.</summary>
public sealed class CoworkHeadTests
{
    /// <summary>Every control on the three views, the rail Projects section and the session-target fields has an accessible name, in keyboard order, and the views respect reduced motion.</summary>
    [Fact]
    public async Task ViewsNameEveryControlAndRespectReducedMotion()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var projects = new ProjectsView();
            var memory = new MemoryView();
            var instructions = new InstructionsView();
            projects.ApplyMotion(true);
            memory.ApplyMotion(true);
            instructions.ApplyMotion(true);
            projects.Classes.Contains(ProjectsView.ReducedMotionClass).ShouldBeTrue();
            memory.Classes.Contains(MemoryView.ReducedMotionClass).ShouldBeTrue();
            instructions.Classes.Contains(InstructionsView.ReducedMotionClass).ShouldBeTrue();

            AssertNamedInOrder(projects.KeyboardOrder, ["Project list", "Project name", "Rename project", "Archive project", "Delete project", "Confirm delete"]);
            AssertNamedInOrder(memory.KeyboardOrder, ["Memory entries", "Delete memory entry", "Memory enabled", "Memory paused", "Include sensitive"]);
            AssertNamedInOrder(instructions.KeyboardOrder, ["Global instructions editor", "Load instructions", "Save instructions"]);
            AutomationProperties.GetName(instructions).ShouldBe("Global instructions");
            instructions.FindControl<TextBlock>("LimitText")!.Text.ShouldBe(GlobalInstructionsModel.LimitMessage);
            var rail = new CoworkRailView();
            AutomationProperties.GetName(rail.ProjectsSection).ShouldBe("Projects");
            var automations = new AutomationsPageView();
            AutomationProperties.GetName(automations.FindControl<StackPanel>("SessionTargets")!).ShouldBe("Session targets");
            AutomationProperties.GetName(automations.FindControl<TextBlock>("SessionNextRun")!).ShouldBe("Next run");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>The rail Projects section opens the editor.</summary>
    [Fact]
    public async Task RailProjectsSectionOpensTheEditor()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var rail = new CoworkRailView();
            rail.ProjectsSection.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            rail.ProjectEditor.Content.ShouldBeOfType<ProjectsView>();
        }, TestContext.Current.CancellationToken);
    }


    /// <summary>Disposing the agent head disposes the Cowork head first: engines stopped and the gateway closed.</summary>
    [Fact]
    public async Task DisposeStopsCoworkBeforeTheCodeHost()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var tree = new TempTree();
            var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, new ScriptedOmp())).ConfigureAwait(true);
            await using var headScope = head.ConfigureAwait(true);
            head.Cowork.ShouldNotBeNull();
            await head.DisposeAsync().ConfigureAwait(true);
            head.Cowork.IsDisposed.ShouldBeTrue();
            head.Cowork.EnginesStopped.ShouldBeTrue();
            head.Cowork.GatewayClosed.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Cowork factory owns its opened session and real gateway until every disposal caller has awaited shutdown.</summary>
    [Fact]
    public async Task CoworkHeadDisposalClosesItsOpenedSessionAndGateway()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var tree = new TempTree();
            var ct = TestContext.Current.CancellationToken;
            var head = await CoworkHead.CreateAsync(tree.State, tree.Workspace, new SignedInGate(), new ScriptedOmp().TransportFactory, null, null, TimeProvider.System, null, null).ConfigureAwait(true);
            await using var headScope = head.ConfigureAwait(true);
            var id = await head.Sessions.CreateSessionAsync(ct).ConfigureAwait(true);
            var row = new AgentRow
            {
                Id = id,
                Title = "task",
                Summary = new SessionSummary { Id = id, Title = "task", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch },
                Status = AgentRowStatus.Ready,
                Group = AgentRowGroup.Idle,
                StatusText = "Ready",
                IconKey = "ready",
                LastActivityText = "now",
                IsLongRunning = false,
                IsStale = false,
                IsLost = false,
                AccessibleName = "task",
            };
            var view = await head.Sessions.OpenSessionAsync(row, ct).ConfigureAwait(true);
            view.Id.ShouldBe(id);
            head.Sessions.ConversationOf(id).ShouldNotBeNull();
            head.Sessions.Notices.ShouldNotContain(static notice => notice.StartsWith(CoworkToolServer.UnavailablePrefix, StringComparison.Ordinal));
            head.EnginesStopped.ShouldBeFalse();
            head.GatewayClosed.ShouldBeFalse();

            var firstDisposal = head.DisposeAsync().AsTask();
            var secondDisposal = head.DisposeAsync().AsTask();
            await Task.WhenAll(firstDisposal, secondDisposal).ConfigureAwait(true);

            head.Sessions.ConversationOf(id).ShouldBeNull();
            head.IsDisposed.ShouldBeTrue();
            head.EnginesStopped.ShouldBeTrue();
            head.GatewayClosed.ShouldBeTrue();
            await Should.ThrowAsync<ObjectDisposedException>(() => head.Sessions.OpenSessionAsync(row, ct)).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private sealed class SignedInGate : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => "acct";
    }

    /// <summary>With the OpenConversation delegate supplied, the selected row shows ViewMapping.Create(model), and null shows the empty state.</summary>
    [Fact]
    public async Task OpenConversationDelegateShowsTheSelectedSessionsConversation()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(() =>
        {
            var catalog = new OneSessionCatalog("task-1");
            var shell = new CoworkShellModel(catalog);
            var model = new CoworkDockContent("conversation");
            var services = new CoworkSurfaceServices
            {
                Catalog = catalog,
                Shell = shell,
                OpenConversation = id => string.Equals(id, "task-1", StringComparison.Ordinal) ? model : null,
            };
            var view = new CoworkView();
            view.Attach(shell);
            view.BindConversation(shell, services);
            shell.Rail.Select("task-1");
            view.Conversation.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe("conversation");
            services = services with { OpenConversation = _ => null };
            view.BindConversation(shell, services);
            view.Conversation.Content.ShouldBeNull();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
    }

    private sealed class OneSessionCatalog(string id) : ICoworkSessionCatalog
    {
        public event EventHandler? Changed { add { } remove { } }

        public IReadOnlyList<CoworkSessionSummary> Snapshot() =>
        [
            new()
            {
                Id = id,
                Title = "task",
                Status = CoworkSessionStatus.Created,
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch,
            },
        ];
    }

    private static void AssertNamedInOrder(IReadOnlyList<Control> controls, IReadOnlyList<string> names)
    {
        controls.Count.ShouldBe(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            AutomationProperties.GetName(controls[i]).ShouldBe(names[i]);
            controls[i].TabIndex.ShouldBe(i + 1);
        }
    }
}
