using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Views.Kanban;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>What one Kanban card draws: copy, kind tag, status as glyph plus words, stale and disconnected words, Substrate fields, fonts and live updates.</summary>
public sealed class KanbanCardViewTests
{
    private static FontFamily Token(string key) => (FontFamily)Application.Current!.FindResource(key)!;

    /// <summary>A card draws its title, kind tag, model and provider, status words, last activity and the Substrate fields as unavailable.</summary>
    [Fact]
    public async Task ACardDrawsItsCopy()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var running = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            var card = fixture.Board.CardOf("run-1").ShouldNotBeNull();

            var texts = ViewHarness.VisibleTexts(running);
            texts.ShouldContain("Build the board", StringComparer.Ordinal);
            texts.ShouldContain("Chat", StringComparer.Ordinal);
            texts.ShouldContain("gpt-5 - openai", StringComparer.Ordinal);
            texts.ShouldContain("Running", StringComparer.Ordinal);
            texts.ShouldContain(card.LastActivityText, StringComparer.Ordinal);
            texts.ShouldContain("Task: unavailable", StringComparer.Ordinal);
            texts.ShouldContain("Graph: unavailable", StringComparer.Ordinal);
            texts.ShouldContain("Lessons: unavailable", StringComparer.Ordinal);
            texts.ShouldContain("Lease: unavailable", StringComparer.Ordinal);
            AutomationProperties.GetName(running).ShouldBe(card.SpokenName);
            running.Status.Text.ShouldBe("Running");
            running.Status.IconKey.ShouldBe(StatusIcon.Running);

            var terminal = fixture.View.CardViewOf("term-1").ShouldNotBeNull();
            ViewHarness.VisibleTexts(terminal).ShouldContain("Terminal", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(terminal).ShouldContain("Model unknown", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Substrate fields show what Substrate reported once the board has read them, and a card without a report stays unavailable.</summary>
    [Fact]
    public async Task SubstrateFieldsShowWhatWasReportedAndNothingElse()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = KanbanFixture.Open((fake, clock) =>
            {
                fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "Build the board"));
                fake.Sessions.Add(KanbanFixture.Session(clock, "run-2", SessionLifecycle.Running, "No report", minutesAgo: 5));
            });
            fixture.Details.Answers["run-1"] = new KanbanCardDetails("Ship the board", "ut-mvp88pnw-6e8c4d24", 4, "agent-swarm", Available: true);

            await fixture.Board.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            fixture.Settle();

            var reported = ViewHarness.VisibleTexts(fixture.View.CardViewOf("run-1")!);
            reported.ShouldContain("Task: Ship the board", StringComparer.Ordinal);
            reported.ShouldContain("Graph: ut-mvp88pnw-6e8c4d24", StringComparer.Ordinal);
            reported.ShouldContain("Lessons: 4", StringComparer.Ordinal);
            reported.ShouldContain("Lease: agent-swarm", StringComparer.Ordinal);
            var silent = ViewHarness.VisibleTexts(fixture.View.CardViewOf("run-2")!);
            silent.ShouldContain("Task: unavailable", StringComparer.Ordinal);
            silent.ShouldContain("Lease: unavailable", StringComparer.Ordinal);
            AutomationProperties.GetName(ViewHarness.Find<StackPanel>(fixture.View.CardViewOf("run-1")!, "SubstrateRow"))
                .ShouldBe("Substrate, Task: Ship the board, Graph: ut-mvp88pnw-6e8c4d24, Lessons: 4, Lease: agent-swarm");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The view reads the Substrate fields itself when it is shown, so a head only has to give the board a source.</summary>
    [Fact]
    public async Task ShowingTheBoardReadsTheSubstrateFields()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open((fake, clock) =>
            {
                fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "Build the board"));
            });

            fixture.Details.Reads.ShouldBeGreaterThan(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A stale card says so in words, a dropped engine says Disconnected, and a failed card does neither: the three are distinct.</summary>
    [Fact]
    public async Task StaleDisconnectedAndFailedAreDistinctInWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open((fake, clock) =>
            {
                fake.Sessions.Add(KanbanFixture.Session(clock, "lost-1", SessionLifecycle.Lost, "Dropped terminal", SessionKind.Terminal));
                fake.Sessions.Add(KanbanFixture.Session(clock, "bad-1", SessionLifecycle.Failed, "Broken run"));
                fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "Quiet run", minutesAgo: 1));
            });
            fixture.Board.SetStale("run-1", true).ShouldBeTrue();
            fixture.Settle();

            var lost = ViewHarness.VisibleTexts(fixture.View.CardViewOf("lost-1")!);
            lost.ShouldContain("Disconnected, the engine dropped", StringComparer.Ordinal);
            lost.ShouldContain("Terminal", StringComparer.Ordinal);
            lost.ShouldNotContain("Failed", StringComparer.Ordinal);
            var failed = ViewHarness.VisibleTexts(fixture.View.CardViewOf("bad-1")!);
            failed.ShouldContain("Failed", StringComparer.Ordinal);
            failed.ShouldNotContain("Disconnected, the engine dropped", StringComparer.Ordinal);
            var stale = ViewHarness.VisibleTexts(fixture.View.CardViewOf("run-1")!);
            stale.ShouldContain("Stale, no recent heartbeat", StringComparer.Ordinal);
            stale.ShouldContain("Running", StringComparer.Ordinal);
            AutomationProperties.GetName(fixture.View.CardViewOf("lost-1")!).ShouldNotBeNull().ShouldContain("Disconnected, the engine dropped", Case.Sensitive);
            AutomationProperties.GetName(fixture.View.CardViewOf("run-1")!).ShouldNotBeNull().ShouldContain("Stale, no recent heartbeat", Case.Sensitive);
            fixture.View.CardViewOf("lost-1")!.Status.IconKey.ShouldBe(StatusIcon.Question);
            fixture.View.CardViewOf("bad-1")!.Status.IconKey.ShouldBe(StatusIcon.Cross);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A closed session stays on the board as a card in a terminal column and says it is closed.</summary>
    [Fact]
    public async Task AClosedSessionStaysAsACardInTheArchivedColumn()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var closed = new HashSet<string>(StringComparer.Ordinal);
            var clock = new ManualTimeProvider();
            var fake = new FakeSessionCatalog();
            fake.Sessions.Add(KanbanFixture.Session(clock, "pause-1", SessionLifecycle.Paused, "Parked work"));
            using var catalog = new SessionCatalogModel(fake, clock, new SessionCatalogOptions { IsClosed = closed.Contains });
            using var board = new KanbanBoardModel(catalog, clock);
            var view = new KanbanBoardView { DataContext = board };
            var window = ViewHarness.Show(view, width: 1400, height: 800);

            closed.Add("pause-1");
            fake.RaiseChanged();
            ViewHarness.Settle(window);

            board.CardOf("pause-1").ShouldNotBeNull().ColumnKey.ShouldBe("archived");
            var card = view.CardViewOf("pause-1").ShouldNotBeNull();
            ViewHarness.VisibleTexts(card).ShouldContain(KanbanCopy.Closed, StringComparer.Ordinal);
            card.GetVisualAncestors().ShouldContain(view.ColumnOf("archived")!);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Card text wraps and uses the Greptile families through the tokens: body for words, mono for ids, display for column titles.</summary>
    [Fact]
    public async Task FontsResolveThroughTheTokensAndTextWraps()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = KanbanFixture.Open((fake, clock) =>
            {
                fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, new string('w', 120) + " a very long title that must wrap inside the column"));
            });
            fixture.Details.Answers["run-1"] = new KanbanCardDetails("Ship", "ut-1", 1, "lease-holder", Available: true);
            await fixture.Board.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            fixture.Settle();
            var family = Token("OmpOS.Font.Family");
            var display = Token("OmpOS.Font.Display");
            var mono = Token("OmpOS.Font.Mono");
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();

            var title = card.TextParts.First();
            title.FontFamily.ShouldBe(family);
            title.TextWrapping.ShouldBe(TextWrapping.Wrap);
            // Spec 4.10: the card's mono model line is one trimmed line, not wrapped.
            var model = card.TextParts.First(static part => string.Equals(part.Name, "ModelText", StringComparison.Ordinal));
            model.TextWrapping.ShouldBe(TextWrapping.NoWrap);
            model.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            card.TextParts.First(static part => string.Equals(part.Name, "GraphField", StringComparison.Ordinal)).FontFamily.ShouldBe(mono);
            card.TextParts.First(static part => string.Equals(part.Name, "LeaseField", StringComparison.Ordinal)).FontFamily.ShouldBe(mono);
            // Spec 4.10: the column header is DM Sans 13/600 (the Display face is kept for the 24 px empty heading only).
            fixture.View.ColumnHeaderOf("running")!.FontFamily.ShouldBe(family);
            fixture.View.StateTexts.First(static block => string.Equals(block.Name, "EmptyHeading", StringComparison.Ordinal)).FontFamily.ShouldBe(display);
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(fixture.View.ColumnOf("running")!.Bounds.Width);

            var allowed = new[] { family, display, mono };
            foreach (var block in fixture.View.GetVisualDescendants().OfType<TextBlock>().Where(static candidate => candidate.TemplatedParent is null && !string.IsNullOrEmpty(candidate.Text)))
            {
                allowed.ShouldContain(block.FontFamily, $"{block.Text} must resolve its family through an OmpOS.Font token");
            }

            card.OpenSteer().ShouldBeTrue();
            fixture.Settle();
            card.SteerInput.FontFamily.ShouldBe(family);
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A status change redraws the chip, moves the card to its new column, keeps the same control, and is announced in the live region.</summary>
    [Fact]
    public async Task AStatusChangeMovesTheCardAndIsAnnounced()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var before = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            before.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("running")!);

            fixture.Board.Apply("run-1", new TestSignal(RuntimeSignalKind.ApprovalRequested));
            fixture.Settle();

            var after = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            after.ShouldBeSameAs(before);
            after.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("needs-attention")!);
            after.Status.Text.ShouldBe("Needs attention");
            after.Status.IconKey.ShouldBe(StatusIcon.Alert);
            fixture.View.Live.Text.ShouldBe("Build the board: Needs attention");
            AutomationProperties.GetLiveSetting(fixture.View.Live).ShouldBe(AutomationLiveSetting.Polite);
            fixture.View.ColumnHeaderOf("running")!.Text.ShouldBe("Running  1");
            fixture.View.ColumnHeaderOf("needs-attention")!.Text.ShouldBe("Needs attention  1");
            AutomationProperties.GetName(after).ShouldBe(fixture.Board.CardOf("run-1")!.SpokenName);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A new session appears as a card at once, and a session that the catalog forgets leaves.</summary>
    [Fact]
    public async Task NewSessionsAppearWithoutARefresh()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            fixture.View.CardViewOf("new-1").ShouldBeNull();

            fixture.Fake.Put(KanbanFixture.Session(fixture.Clock, "new-1", SessionLifecycle.Created, "Fresh session", SessionKind.Terminal));
            fixture.Settle();

            var card = fixture.View.CardViewOf("new-1").ShouldNotBeNull();
            card.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("queued")!);
            ViewHarness.VisibleTexts(card).ShouldContain("Terminal", StringComparer.Ordinal);
            fixture.View.CardViews.Count.ShouldBe(5);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The running glyph pulses under full motion and holds still under reduced motion.</summary>
    [Fact]
    public async Task TheRunningGlyphHonoursReducedMotion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var full = KanbanFixture.Open();
            ViewHarness.PulsingIcons(full.View).ShouldBeGreaterThan(0);

            using var reduced = KanbanFixture.Open(reducedMotion: true);
            ViewHarness.PulsingIcons(reduced.View).ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Activating a card by double tap asks the owner to open its session.</summary>
    [Fact]
    public async Task ActivatingACardRequestsItsSession()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var opened = new List<string?>();
            fixture.Board.OpenSessionRequested += (_, args) => opened.Add(args.SessionId);

            fixture.View.CardViewOf("done-1")!.Model!.OpenCommand.Execute(null);

            opened.ShouldBe(["done-1"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
