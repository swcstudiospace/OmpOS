using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Views.Kanban;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>The steer flyout of a card: the message box, Send now and Queue for next turn, and the result line.</summary>
public sealed class KanbanSteerFlyoutTests
{
    private static Task PressAsync(Button button) => ((AgentPanelCommand)button.Command!).ExecuteAsync(button.CommandParameter);

    /// <summary>The flyout names its controls, makes both buttons at least the WCAG 2.2 minimum (24 px) high and disables them while the box is blank.</summary>
    [Fact]
    public async Task TheFlyoutIsNamedAndDisabledWhileBlank()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();

            ViewHarness.Click(fixture.Window, card.SteerTrigger);

            card.IsSteerOpen.ShouldBeTrue("the Steer button opens the flyout");
            AutomationProperties.GetName(card.SteerInput).ShouldBe(KanbanCopy.SteerBoxName);
            AutomationProperties.GetName(card.SendNowTrigger).ShouldBe(KanbanCopy.SendNowName);
            AutomationProperties.GetName(card.QueueTrigger).ShouldBe(KanbanCopy.QueueNextName);
            card.SendNowTrigger.Content.ShouldBe(KanbanCopy.SendNow);
            card.QueueTrigger.Content.ShouldBe(KanbanCopy.QueueNext);
            card.SendNowTrigger.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            card.QueueTrigger.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            card.SendNowTrigger.Classes.Contains("primary").ShouldBeTrue();
            card.SendNowTrigger.IsEffectivelyEnabled.ShouldBeFalse();
            card.QueueTrigger.IsEffectivelyEnabled.ShouldBeFalse();
            card.SteerResultLine.IsVisible.ShouldBeFalse();
            AutomationProperties.GetLiveSetting(card.SteerResultLine).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(card.SteerResultLine).ShouldBe(KanbanCopy.SteerResultName);
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Send now calls the steering service for that card's session in send-now mode, clears the box and shows the result.</summary>
    [Fact]
    public async Task SendNowSteersTheSessionOfTheCard()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = KanbanFixture.Open();
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            card.OpenSteer().ShouldBeTrue();
            fixture.Settle();

            card.SteerInput.Text = "use the other table";
            fixture.Settle();
            card.Model!.SteerText.ShouldBe("use the other table", "the box is two-way bound");
            card.SendNowTrigger.IsEffectivelyEnabled.ShouldBeTrue();
            await PressAsync(card.SendNowTrigger).ConfigureAwait(true);
            fixture.Settle();

            fixture.Steering.Calls.ShouldBe([new FakeCardSteering.SteerCall("run-1", "use the other table", CardSteerMode.SendNow)]);
            card.SteerInput.Text.ShouldBeNullOrEmpty();
            card.SteerResultLine.IsVisible.ShouldBeTrue();
            card.SteerResultLine.Text.ShouldBe("Sent to the session");
            card.SteerResultLine.Classes.Contains("tone-error").ShouldBeFalse();
            fixture.View.Live.Text.ShouldBe("Build the board: Sent to the session");
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Queue for next turn calls the service in follow-up mode and says it queued.</summary>
    [Fact]
    public async Task QueueForNextTurnSendsAFollowUp()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = KanbanFixture.Open();
            fixture.Steering.Answer = static _ => CardSteerResult.Queued();
            var card = fixture.View.CardViewOf("term-1").ShouldNotBeNull();
            card.OpenSteer().ShouldBeTrue();
            card.SteerInput.Text = "then run the tests";
            fixture.Settle();

            await PressAsync(card.QueueTrigger).ConfigureAwait(true);
            fixture.Settle();

            fixture.Steering.Calls.ShouldBe([new FakeCardSteering.SteerCall("term-1", "then run the tests", CardSteerMode.FollowUp)]);
            card.SteerResultLine.Text.ShouldBe("Queued for the next turn");
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A refused message stays in the box, and the result line says why in error colour and in words.</summary>
    [Fact]
    public async Task AFailedSteerKeepsTheTextAndShowsTheReason()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = KanbanFixture.Open();
            fixture.Steering.Answer = static _ => CardSteerResult.Failed("The terminal dropped, so the message was not sent");
            var card = fixture.View.CardViewOf("lost-1").ShouldNotBeNull();
            card.OpenSteer().ShouldBeTrue();
            card.SteerInput.Text = "are you there";
            fixture.Settle();

            await PressAsync(card.SendNowTrigger).ConfigureAwait(true);
            fixture.Settle();

            card.SteerInput.Text.ShouldBe("are you there");
            card.Model!.SteerText.ShouldBe("are you there");
            card.SteerResultLine.Text.ShouldBe("The terminal dropped, so the message was not sent");
            card.SteerResultLine.Classes.Contains("tone-error").ShouldBeTrue();
            fixture.Board.CardOf("lost-1")!.Status.ShouldBe(RuntimeStatus.Unreachable, "a refused message leaves the session unchanged");
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The message typed into a flyout survives another session's status change on the board.</summary>
    [Fact]
    public async Task TypedTextSurvivesAnotherCardsUpdate()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            card.OpenSteer().ShouldBeTrue();
            card.SteerInput.Text = "half a thought";
            fixture.Settle();

            fixture.Board.Apply("term-1", new TestSignal(RuntimeSignalKind.InputRequested));
            fixture.Settle();

            fixture.View.CardViewOf("run-1").ShouldBeSameAs(card);
            card.IsSteerOpen.ShouldBeTrue("a change to another card does not close the flyout");
            card.SteerInput.Text.ShouldBe("half a thought");
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
