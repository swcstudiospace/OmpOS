using Bunit;
using Microsoft.AspNetCore.Components;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Autonogrammer.Parity.Support;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components;
using Autonogrammer.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// Razor assertions for session-chat parity rows that the shared scenarios and the existing Razor tests do not
/// assert: placeholders, affordance callbacks, the skip-link target, and the composer controls.
/// </summary>
public sealed class SessionChatParityGapTests : UiTestContext
{
    /// <summary>History that is still loading shows the loading placeholder and no blocks.</summary>
    [Fact]
    public void LoadingHistoryShowsPlaceholder()
    {
        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.LoadingHistory));

        view.FindAll(".autonogrammer-loading").Count.ShouldBe(1);
        view.FindAll(".autonogrammer-blocks").ShouldBeEmpty();
        view.FindAll(".autonogrammer-empty").ShouldBeEmpty();
    }

    /// <summary>A loaded conversation with no messages shows the empty prompt and no blocks.</summary>
    [Fact]
    public void EmptyConversationShowsPrompt()
    {
        var model = new SessionViewModel();
        model.MarkHistoryLoaded();

        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, model.Tree)
            .Add(p => p.ViewState, model.State));

        model.State.ShouldBe(SessionViewState.Empty);
        view.FindAll(".autonogrammer-empty").Count.ShouldBe(1);
        view.FindAll(".autonogrammer-blocks").ShouldBeEmpty();
    }

    /// <summary>The retry control on a retryable error block invokes the retry callback.</summary>
    [Fact]
    public void ErrorRetryButtonInvokesRetry()
    {
        var retries = 0;
        var view = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new ErrorBlock { Id = "error-1", Message = "transient", Retryable = true })
            .Add(p => p.OnRetry, EventCallback.Factory.Create(this, () => retries++)));

        view.Find("button[aria-label='Retry failed operation']").Click();

        retries.ShouldBe(1);
    }

    /// <summary>A fatal error shows its message in an alert and offers no retry control.</summary>
    [Fact]
    public void FatalErrorRendersAlertWithoutRetry()
    {
        var view = Render<UiBlockRenderer>(parameters => parameters
            .Add(p => p.Block, new ErrorBlock { Id = "error-2", Message = "Model unavailable", Retryable = false }));

        view.Find("[role='alert'] .autonogrammer-error-message").TextContent.ShouldBe("Model unavailable");
        view.FindAll("button").ShouldBeEmpty();
    }

    /// <summary>While streaming, the session's stop control invokes the stop callback.</summary>
    [Fact]
    public void StopAffordanceInvokesOnStop()
    {
        var model = ModelAfter(ParityEvents.Delta("working"));
        model.State.ShouldBe(SessionViewState.Streaming);
        var stops = 0;

        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, model.Tree)
            .Add(p => p.ViewState, model.State)
            .Add(p => p.OnStop, EventCallback.Factory.Create(this, () => stops++)));

        view.Find("section > button[aria-label='Stop response']").Click();

        stops.ShouldBe(1);
    }

    /// <summary>After a recoverable error, the session's retry control invokes the retry callback.</summary>
    [Fact]
    public void RetryAffordanceInvokesOnRetry()
    {
        var model = ModelAfter(ParityEvents.Delta("partial"), ParityEvents.Error("transient", retryable: true));
        model.State.ShouldBe(SessionViewState.RecoverableError);
        var retries = 0;

        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, model.Tree)
            .Add(p => p.ViewState, model.State)
            .Add(p => p.OnRetry, EventCallback.Factory.Create(this, () => retries++)));

        view.Find("section > button[aria-label='Retry failed operation']").Click();

        retries.ShouldBe(1);
    }

    /// <summary>The skip link targets the session section, which accepts programmatic focus.</summary>
    [Fact]
    public void SkipLinkTargetIsFocusableSessionSection()
    {
        var view = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Success));

        var target = view.Find("#autonogrammer-conversation");

        target.LocalName.ShouldBe("section");
        target.GetAttribute("tabindex").ShouldBe("-1");
    }

    /// <summary>When not streaming, the composer's send control is named by its label and no stop control appears.</summary>
    [Fact]
    public void SendButtonIsNamedByItsLabel()
    {
        var view = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, _ => Task.FromResult(true)));

        view.FindAll("button[aria-label='Send message']").Count.ShouldBe(1);
        view.FindAll("button[aria-label='Stop response']").ShouldBeEmpty();
    }

    /// <summary>While streaming, the composer's stop control replaces send and invokes the stop callback.</summary>
    [Fact]
    public void ComposerStopButtonInvokesOnStop()
    {
        var stops = 0;
        var view = Render<Composer>(parameters => parameters
            .Add(p => p.IsStreaming, true)
            .Add(p => p.OnSend, _ => Task.FromResult(true))
            .Add(p => p.OnStop, EventCallback.Factory.Create(this, () => stops++)));

        view.FindAll("button[aria-label='Send message']").ShouldBeEmpty();
        view.Find("button[aria-label='Stop response']").Click();

        stops.ShouldBe(1);
    }

    private static SessionViewModel ModelAfter(params StreamEventPayload[] payloads)
    {
        var model = new SessionViewModel();
        foreach (var evt in ParityEvents.Sequence(payloads))
        {
            model.ApplyEvent(evt);
        }

        return model;
    }
}
