using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.SideChats;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.SideChats;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.SideChats;

/// <summary>The side chat pane: a card per open side chat with its messages, the ask box and its keys, the answering and failure lines, Close, New side chat, the optional Fork button, and the accessibility contract.</summary>
public sealed class SideChatPaneViewTests
{
    private const string NewQuestionName = "First question of a new side chat";
    private const string NewChatName = "New side chat";
    private const string ForkName = "Fork this chat into a new session. The original chat is unchanged.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string AskName(string title) => "Ask a question in " + title;

    private static string SendName(string title) => "Send your question in " + title;

    private static async Task RunAsync(ViewSideChatEngine engine, ViewSessionForker? forker, Func<SideChatHub, SideChatPaneView, Window, Task> body)
    {
        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(async () =>
        {
            var hub = new SideChatHub(new SideChatPanel("parent"), engine, static () => []);
            var view = new SideChatPaneView { DataContext = hub, Fork = forker is null ? null : new ForkModel("parent", forker) };
            var window = new Window { Width = 900, Height = 2400, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                await body(hub, view, window).ConfigureAwait(true);
            }
            finally
            {
                window.Close();
            }
        }, Token).ConfigureAwait(true);
    }

    private static async Task<SideChatModel> OpenAsync(SideChatHub hub, Window window, string question = "")
    {
        var chat = await hub.OpenAsync(question, Token).ConfigureAwait(true);
        SettingsTree.SettleSafe(window);
        return chat;
    }

    /// <summary>With no side chat open the pane says so, offers New side chat, hides Fork while it has no fork model, and every control is named.</summary>
    [Fact]
    public async Task AnEmptyPaneOffersANewSideChatAndHidesFork()
    {
        await RunAsync(new ViewSideChatEngine(), null, (hub, view, window) =>
        {
            view.Chats.ItemCount.ShouldBe(0);
            view.Empty.IsVisible.ShouldBeTrue();
            view.ForkChat.IsVisible.ShouldBeFalse();
            view.ForkStatus.IsVisible.ShouldBeFalse();
            view.HubStatus.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(view.NewSideChat).ShouldBe(NewChatName);
            AutomationProperties.GetName(view.NewQuestion).ShouldBe(NewQuestionName);
            SettingsTree.Unnamed(window).ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>New side chat with a typed question opens a card, asks the question, shows both messages, clears the box and puts focus in the card's ask box.</summary>
    [Fact]
    public async Task NewSideChatOpensACardAndAsksTheTypedQuestion()
    {
        var engine = new ViewSideChatEngine();
        await RunAsync(engine, null, (hub, view, window) =>
        {
            view.NewQuestion.Text = "what is a token?";

            SettingsTree.Click(window, view.NewSideChat);

            hub.Open.Count.ShouldBe(1);
            engine.Requests.ShouldHaveSingleItem().Question.ShouldBe("what is a token?");
            view.Chats.ItemCount.ShouldBe(1);
            view.Empty.IsVisible.ShouldBeFalse();
            string.IsNullOrEmpty(view.NewQuestion.Text).ShouldBeTrue();
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain("Side chat 1", StringComparer.Ordinal);
            texts.ShouldContain(SideChatCopy.Notice, StringComparer.Ordinal);
            texts.ShouldContain("what is a token?", StringComparer.Ordinal);
            texts.ShouldContain("an answer", StringComparer.Ordinal);
            texts.ShouldContain(nameof(SideChatAuthor.User), StringComparer.Ordinal);
            texts.ShouldContain(nameof(SideChatAuthor.Assistant), StringComparer.Ordinal);
            SettingsTree.FocusedName(window).ShouldBe(AskName("Side chat 1"));
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Enter in the first-question box opens a side chat; with no question nothing is asked.</summary>
    [Fact]
    public async Task EnterInTheFirstQuestionBoxOpensAnEmptySideChat()
    {
        var engine = new ViewSideChatEngine();
        await RunAsync(engine, null, (hub, view, window) =>
        {
            _ = view.NewQuestion.Focus();

            SettingsTree.Press(window, Key.Enter, PhysicalKey.Enter);

            hub.Open.Count.ShouldBe(1);
            engine.Requests.ShouldBeEmpty();
            view.Chats.ItemCount.ShouldBe(1);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Enter in the ask box sends the question and clears the box; Shift+Enter sends nothing and keeps the text.</summary>
    [Fact]
    public async Task EnterSendsAndShiftEnterDoesNot()
    {
        var engine = new ViewSideChatEngine();
        await RunAsync(engine, null, async (hub, view, window) =>
        {
            var chat = await OpenAsync(hub, window).ConfigureAwait(true);
            var box = view.AskBoxOf(chat)!;

            box.Text = "draft";
            _ = box.Focus();
            SettingsTree.Press(window, Key.Enter, PhysicalKey.Enter, RawInputModifiers.Shift);
            engine.Requests.ShouldBeEmpty();
            box.Text.ShouldNotBeNullOrEmpty();

            box.Text = "hello";
            SettingsTree.Press(window, Key.Enter, PhysicalKey.Enter);

            engine.Requests.ShouldHaveSingleItem().Question.ShouldBe("hello");
            string.IsNullOrEmpty(box.Text).ShouldBeTrue();
            chat.Chat.Messages.Count.ShouldBe(2);
            SettingsTree.Texts(view).ShouldContain("hello", StringComparer.Ordinal);
        }).ConfigureAwait(true);
    }

    /// <summary>The Send button asks the question through the model; a blank question is refused on an assertive failure line and nothing is sent.</summary>
    [Fact]
    public async Task SendAsksAndABlankQuestionIsRefusedAtOnce()
    {
        var engine = new ViewSideChatEngine();
        await RunAsync(engine, null, async (hub, view, window) =>
        {
            var chat = await OpenAsync(hub, window).ConfigureAwait(true);

            SettingsTree.Click(window, view.SendOf(chat)!);

            engine.Requests.ShouldBeEmpty();
            var error = view.ErrorOf(chat)!;
            error.IsVisible.ShouldBeTrue();
            error.Text.ShouldBe(SideChatCopy.BlankQuestion);
            AutomationProperties.GetLiveSetting(error).ShouldBe(AutomationLiveSetting.Assertive);

            view.AskBoxOf(chat)!.Text = "now a question";
            SettingsTree.Click(window, SettingsTree.Named<Button>(view, SendName("Side chat 1")));

            engine.Requests.ShouldHaveSingleItem().Question.ShouldBe("now a question");
            view.ErrorOf(chat)!.IsVisible.ShouldBeFalse();
        }).ConfigureAwait(true);
    }

    /// <summary>While the assistant answers the card says so on a polite line; when the answer arrives the line goes and the answer shows.</summary>
    [Fact]
    public async Task AnAnsweringIndicatorShowsWhileTheAssistantAnswers()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new ViewSideChatEngine { Gate = gate };
        await RunAsync(engine, null, async (hub, view, window) =>
        {
            var chat = await OpenAsync(hub, window).ConfigureAwait(true);
            view.AskBoxOf(chat)!.Text = "slow question";

            SettingsTree.Click(window, view.SendOf(chat)!);

            chat.IsAnswering.ShouldBeTrue();
            var answering = view.AnsweringOf(chat)!;
            answering.IsVisible.ShouldBeTrue();
            answering.Text.ShouldBe(SideChatCopy.Answering);
            AutomationProperties.GetLiveSetting(answering).ShouldBe(AutomationLiveSetting.Polite);
            string.IsNullOrEmpty(view.AskBoxOf(chat)!.Text).ShouldBeTrue();

            gate.SetResult("the slow answer");
            SettingsTree.SettleSafe(window);

            chat.IsAnswering.ShouldBeFalse();
            view.AnsweringOf(chat)!.IsVisible.ShouldBeFalse();
            SettingsTree.Texts(view).ShouldContain("the slow answer", StringComparer.Ordinal);
        }).ConfigureAwait(true);
    }

    /// <summary>An engine failure is announced on the assertive line and the question comes back into the box so it can be asked again.</summary>
    [Fact]
    public async Task AFailedAnswerIsAnnouncedAndTheQuestionComesBack()
    {
        var engine = new ViewSideChatEngine { Answer = null };
        await RunAsync(engine, null, async (hub, view, window) =>
        {
            var chat = await OpenAsync(hub, window).ConfigureAwait(true);
            view.AskBoxOf(chat)!.Text = "will fail";

            SettingsTree.Click(window, view.SendOf(chat)!);

            var error = view.ErrorOf(chat)!;
            error.IsVisible.ShouldBeTrue();
            error.Text.ShouldBe(SideChatCopy.Failed("engine offline"));
            AutomationProperties.GetLiveSetting(error).ShouldBe(AutomationLiveSetting.Assertive);
            view.AskBoxOf(chat)!.Text.ShouldBe("will fail");
        }).ConfigureAwait(true);
    }

    /// <summary>Close ends one side chat and removes its card; the other card stays with its draft, and focus moves to New side chat.</summary>
    [Fact]
    public async Task CloseRemovesOnlyThatCardAndKeepsTheOthersDraft()
    {
        var engine = new ViewSideChatEngine();
        await RunAsync(engine, null, async (hub, view, window) =>
        {
            var first = await OpenAsync(hub, window).ConfigureAwait(true);
            var second = await OpenAsync(hub, window).ConfigureAwait(true);
            view.Chats.ItemCount.ShouldBe(2);
            view.AskBoxOf(first)!.Text = "my draft";

            SettingsTree.Click(window, view.CloseOf(second)!);

            engine.Closed.ShouldBe([second.Id]);
            hub.Open.ShouldHaveSingleItem().ShouldBeSameAs(first);
            view.Chats.ItemCount.ShouldBe(1);
            view.AskBoxOf(first)!.Text.ShouldBe("my draft");
            SettingsTree.Texts(view).ShouldNotContain("Side chat 2", StringComparer.Ordinal);
            SettingsTree.FocusedName(window).ShouldBe(NewChatName);
        }).ConfigureAwait(true);
    }

    /// <summary>The Fork button shows only while a fork model is set, duplicates the session and announces the new session politely; a failed fork is assertive.</summary>
    [Fact]
    public async Task ForkDuplicatesTheSessionAndReportsTheOutcome()
    {
        var forker = new ViewSessionForker();
        await RunAsync(new ViewSideChatEngine(), forker, (hub, view, window) =>
        {
            view.ForkChat.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(view.ForkChat).ShouldBe(ForkName);
            var forked = 0;
            view.Fork!.Forked += (_, _) => forked++;

            SettingsTree.Click(window, view.ForkChat);

            forker.Requests.ShouldHaveSingleItem().ShouldBe(new ForkRequest("parent", null));
            forked.ShouldBe(1);
            view.ForkStatus.IsVisible.ShouldBeTrue();
            view.ForkStatus.Text.ShouldBe(ForkCopy.Forked("Copy of chat", null));
            AutomationProperties.GetLiveSetting(view.ForkStatus).ShouldBe(AutomationLiveSetting.Polite);

            forker.Failure = new InvalidOperationException("engine refused");
            SettingsTree.Click(window, view.ForkChat);

            view.ForkStatus.Text.ShouldBe(ForkCopy.Failed("engine refused"));
            AutomationProperties.GetLiveSetting(view.ForkStatus).ShouldBe(AutomationLiveSetting.Assertive);
            view.ForkStatus.Classes.Contains("tone-error").ShouldBeTrue();

            view.Fork = null;
            SettingsTree.SettleSafe(window);
            view.ForkChat.IsVisible.ShouldBeFalse();
            view.ForkStatus.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>The hub's refusal to nest side chats is shown on the pane's status line.</summary>
    [Fact]
    public async Task TheHubStatusIsShown()
    {
        await RunAsync(new ViewSideChatEngine(), null, async (hub, view, window) =>
        {
            var chat = await OpenAsync(hub, window).ConfigureAwait(true);

            hub.OpenFrom(chat).ShouldBeNull();
            SettingsTree.SettleSafe(window);

            view.HubStatus.IsVisible.ShouldBeTrue();
            view.HubStatus.Text.ShouldBe(SideChatCopy.NoNesting);
        }).ConfigureAwait(true);
    }

    /// <summary>Tab walks the new-chat controls and then the card in the order they are drawn.</summary>
    [Fact]
    public async Task TabFollowsTheOrderOfTheControls()
    {
        await RunAsync(new ViewSideChatEngine(), new ViewSessionForker(), async (hub, view, window) =>
        {
            _ = await OpenAsync(hub, window).ConfigureAwait(true);

            var order = SettingsTree.TabStops(view)
                .Where(static control => control is Button or TextBox or ScrollViewer)
                .Select(static control => AutomationProperties.GetName(control))
                .ToArray();

            order.ShouldBe(
            [
                NewQuestionName,
                NewChatName,
                ForkName,
                SideChatCopy.CloseName,
                "Messages of Side chat 1",
                AskName("Side chat 1"),
                SendName("Side chat 1"),
            ]);
        }).ConfigureAwait(true);
    }

    /// <summary>With an answering card, a failed card and a fork status shown, every control and live region has an accessible name and every control meets the target size.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnough()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new ViewSideChatEngine { Answer = null };
        await RunAsync(engine, new ViewSessionForker(), async (hub, view, window) =>
        {
            var failed = await OpenAsync(hub, window).ConfigureAwait(true);
            view.AskBoxOf(failed)!.Text = "will fail";
            SettingsTree.Click(window, view.SendOf(failed)!);
            engine.Gate = gate;
            var answering = await OpenAsync(hub, window).ConfigureAwait(true);
            view.AskBoxOf(answering)!.Text = "will wait";
            SettingsTree.Click(window, view.SendOf(answering)!);
            SettingsTree.Click(window, view.ForkChat);

            view.ErrorOf(failed)!.IsVisible.ShouldBeTrue();
            view.AnsweringOf(answering)!.IsVisible.ShouldBeTrue();
            view.ForkStatus.IsVisible.ShouldBeTrue();
            SettingsTree.Unnamed(window).ShouldBeEmpty();

            var controls = SettingsTree.Visible<Button>(view).Cast<Control>()
                .Concat(SettingsTree.Visible<TextBox>(view))
                .Where(static control => control.TemplatedParent is null)
                .ToArray();
            controls.Length.ShouldBeGreaterThan(6);
            foreach (var control in controls)
            {
                var name = AutomationProperties.GetName(control);
                name.ShouldNotBeNullOrWhiteSpace();
                control.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too short");
                control.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too narrow");
            }

            gate.SetResult("released");
            SettingsTree.SettleSafe(window);
        }).ConfigureAwait(true);
    }
}
