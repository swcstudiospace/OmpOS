using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;
using static OmpOS.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace OmpOS.Presentation.Tests.Kanban;

/// <summary>The steer entry point of a card: it calls the steering service for that session, clears the text on success and keeps it on failure.</summary>
public sealed class KanbanCardSteeringTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();
    private readonly FakeCardSteering _steering = new();

    /// <summary>A delivered message goes to the session of the card, clears the box and says so.</summary>
    [Fact]
    public async Task ASentMessageClearsTheTextAndSaysDelivered()
    {
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "  use the other table  ";

        await model.SteerCommand.ExecuteAsync(CardSteerMode.SendNow).ConfigureAwait(true);

        _steering.Calls.ShouldBe([new FakeCardSteering.SteerCall("a", "use the other table", CardSteerMode.SendNow)]);
        model.SteerText.ShouldBeEmpty();
        model.SteerResultText.ShouldBe("Sent to the session");
        model.SteerFailed.ShouldBeFalse();
        board.Announcement.ShouldBe("Build: Sent to the session");
    }

    /// <summary>A follow-up passes its mode and reports that it queued.</summary>
    [Fact]
    public async Task AFollowUpQueuesAndSaysQueued()
    {
        _steering.Answer = static _ => CardSteerResult.Queued();
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "then run the tests";

        await model.SteerCommand.ExecuteAsync(CardSteerMode.FollowUp).ConfigureAwait(true);

        _steering.Calls.Single().Mode.ShouldBe(CardSteerMode.FollowUp);
        model.SteerText.ShouldBeEmpty();
        model.SteerResultText.ShouldBe("Queued for the next turn");
    }

    /// <summary>A refused message keeps the text, says why in the service's words, and flags the failure.</summary>
    [Fact]
    public async Task ARefusedMessageKeepsTheTextAndSaysWhy()
    {
        _steering.Answer = static _ => CardSteerResult.Failed("The session ended, so it cannot take a message");
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "please stop";

        await model.SteerCommand.ExecuteAsync(null).ConfigureAwait(true);

        model.SteerText.ShouldBe("please stop");
        model.SteerFailed.ShouldBeTrue();
        model.SteerResultText.ShouldBe("The session ended, so it cannot take a message");
        _steering.Calls.Single().Mode.ShouldBe(CardSteerMode.SendNow, "a null parameter means send now");
        board.Announcement.ShouldBe("Build: The session ended, so it cannot take a message");

        _steering.Answer = static _ => CardSteerResult.Delivered();
        await model.SteerCommand.ExecuteAsync(null).ConfigureAwait(true);
        model.SteerText.ShouldBeEmpty();
        model.SteerFailed.ShouldBeFalse();
    }

    /// <summary>A service that throws is a failure with its message, and the text stays.</summary>
    [Fact]
    public async Task AThrowingServiceKeepsTheText()
    {
        _steering.Answer = static _ => throw new InvalidOperationException("The terminal dropped");
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "hello";

        await model.SteerCommand.ExecuteAsync(null).ConfigureAwait(true);

        model.SteerText.ShouldBe("hello");
        model.SteerFailed.ShouldBeTrue();
        model.SteerResultText.ShouldBe("The terminal dropped");
        model.IsSteering.ShouldBeFalse();
    }

    /// <summary>A blank message is not sent, the command is disabled, and a head without a steering service cannot steer.</summary>
    [Fact]
    public async Task ABlankMessageOrNoServiceDoesNotSend()
    {
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerCommand.CanExecute(null).ShouldBeFalse();
        model.SteerText = "   ";
        model.SteerCommand.CanExecute(null).ShouldBeFalse();
        await model.SteerCommand.ExecuteAsync(null).ConfigureAwait(true);
        _steering.Calls.ShouldBeEmpty();

        model.SteerText = "go";
        model.SteerCommand.CanExecute(null).ShouldBeTrue();

        using var bare = new KanbanBoardModel(catalog, _clock);
        var bareModel = bare.CardModelOf("a").ShouldNotBeNull();
        bareModel.CanSteer.ShouldBeFalse();
        bareModel.SteerText = "go";
        bareModel.SteerCommand.CanExecute(null).ShouldBeFalse();
    }

    /// <summary>While a send runs the command is disabled; the text is cleared only when it ends.</summary>
    [Fact]
    public async Task ASendInProgressDisablesTheCommand()
    {
        var gate = new TaskCompletionSource();
        _steering.Gate = gate.Task;
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "wait for me";

        var sending = model.SteerCommand.ExecuteAsync(null);

        model.IsSteering.ShouldBeTrue();
        model.SteerCommand.CanExecute(null).ShouldBeFalse();
        model.SteerText.ShouldBe("wait for me");

        gate.SetResult();
        await sending.ConfigureAwait(true);

        model.IsSteering.ShouldBeFalse();
        model.SteerText.ShouldBeEmpty();
    }

    /// <summary>Text typed into the box survives a status change of its card, because the view model is kept.</summary>
    [Fact]
    public void TypedTextSurvivesAStatusChange()
    {
        using var catalog = NewCatalog();
        using var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "half a thought";

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.InputRequested));

        board.CardModelOf("a").ShouldBeSameAs(model);
        model.SteerText.ShouldBe("half a thought");
    }

    /// <summary>Disposing the board cancels a send in flight.</summary>
    [Fact]
    public async Task DisposingTheBoardCancelsASendInFlight()
    {
        var gate = new TaskCompletionSource();
        _steering.Gate = gate.Task;
        using var catalog = NewCatalog();
        var board = NewBoard(catalog);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        model.SteerText = "hello";
        var sending = model.SteerCommand.ExecuteAsync(null);
        _steering.Tokens.Single().IsCancellationRequested.ShouldBeFalse();

        board.Dispose();
        await sending.ConfigureAwait(true);

        _steering.Tokens.Single().IsCancellationRequested.ShouldBeTrue();
        model.SteerText.ShouldBe("hello");
        model.IsSteering.ShouldBeFalse();
    }

    private SessionCatalogModel NewCatalog()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Running, now.AddMinutes(-10), now.AddMinutes(-1), "Build"));
        return new SessionCatalogModel(_catalog, _clock);
    }

    private KanbanBoardModel NewBoard(SessionCatalogModel catalog) =>
        new(catalog, _clock, new KanbanBoardOptions { Steering = _steering });
}
