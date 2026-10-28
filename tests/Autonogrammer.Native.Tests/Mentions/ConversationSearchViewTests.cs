using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Mentions;

/// <summary>The conversation search overlay: hidden until opened, takes the focus into the query box, filters as the user types, and opens, or dismisses, from the keyboard.</summary>
public sealed class ConversationSearchViewTests
{
    private sealed class Rig : IDisposable
    {
        internal Rig(ConversationSearchModel model, ConversationSearchView view, Window window)
        {
            Model = model;
            View = view;
            Window = window;
        }

        internal ConversationSearchModel Model { get; }

        internal ConversationSearchView View { get; }

        internal Window Window { get; }

        public void Dispose() => Window.Close();
    }

    private static Rig Open()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("s1", "Fix login", "the login bug is in the session store");
        index.Upsert("s2", "Release notes", "login page redesign");
        var model = new ConversationSearchModel(index);
        var view = new ConversationSearchView { DataContext = model };
        var window = ViewHarness.Show(view, width: 700, height: 500);
        return new Rig(model, view, window);
    }

    private static void Type(Rig rig, string text)
    {
        rig.View.Query.Text = text;
        ViewHarness.Settle(rig.Window);
    }

    /// <summary>The overlay is hidden until the model opens; opening shows the prompt text, names every control and takes the focus into the query box.</summary>
    [Fact]
    public async Task OpeningShowsThePromptAndFocusesTheQuery()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            rig.View.IsVisible.ShouldBeFalse();

            rig.Model.Open();
            ViewHarness.Settle(rig.Window);

            rig.View.IsVisible.ShouldBeTrue();
            rig.View.Query.IsFocused.ShouldBeTrue();
            rig.View.Empty.Text.ShouldBe(ConversationSearchCopy.Prompt);
            AutomationProperties.GetName(rig.View.Query).ShouldBe(ConversationSearchCopy.QueryName);
            AutomationProperties.GetName(rig.View.Results).ShouldBe(ConversationSearchCopy.ListName);
            AutomationProperties.GetLiveSetting(rig.View.CountLine).ShouldBe(AutomationLiveSetting.Polite);
            ViewHarness.Unnamed(rig.View).ShouldBeEmpty();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Typing lists the matching conversations with their titles and snippets and announces the count; a query nobody matches says so.</summary>
    [Fact]
    public async Task TypingListsMatchesAndSaysWhenThereAreNone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            rig.Model.Open();

            Type(rig, "login");

            rig.View.Results.ItemCount.ShouldBe(2);
            rig.View.CountLine.Text.ShouldBe("2 conversations");
            rig.View.Empty.IsVisible.ShouldBeFalse();
            var texts = ViewHarness.VisibleTexts(rig.View.Results);
            texts.ShouldContain("Fix login", StringComparer.Ordinal);
            texts.Any(static text => text.Contains("session store", StringComparison.Ordinal)).ShouldBeTrue();

            Type(rig, "zebra");

            rig.View.Results.IsVisible.ShouldBeFalse();
            rig.View.Empty.IsVisible.ShouldBeTrue();
            rig.View.Empty.Text.ShouldNotBeNull().ShouldContain("zebra", Case.Sensitive);
            rig.View.CountLine.Text.ShouldBe("No results");
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down moves the highlight while focus stays in the box; Enter opens the highlighted conversation, closes the overlay and raises the session.</summary>
    [Fact]
    public async Task ArrowsAndEnterOpenAConversation()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            var chosen = new List<string?>();
            rig.View.SessionChosen += (_, args) => chosen.Add(args.SessionId);
            rig.Model.Open();
            Type(rig, "login");

            ViewHarness.Press(rig.Window, Key.Down, PhysicalKey.ArrowDown);
            rig.View.Results.SelectedIndex.ShouldBe(1);
            rig.View.Query.IsFocused.ShouldBeTrue();

            ViewHarness.Press(rig.Window, Key.Enter, PhysicalKey.Enter);

            chosen.ShouldBe(["s2"]);
            rig.Model.IsOpen.ShouldBeFalse();
            rig.View.IsVisible.ShouldBeFalse();
            rig.View.Query.Text.ShouldBeNullOrEmpty();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape closes the overlay and tells the host so it can return the focus.</summary>
    [Fact]
    public async Task EscapeDismisses()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            var dismissed = 0;
            rig.View.Dismissed += (_, _) => dismissed++;
            rig.Model.Open();
            Type(rig, "login");

            ViewHarness.Press(rig.Window, Key.Escape, PhysicalKey.Escape);

            dismissed.ShouldBe(1);
            rig.Model.IsOpen.ShouldBeFalse();
            rig.View.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter with nothing selected raises nothing and keeps the overlay open.</summary>
    [Fact]
    public async Task EnterWithNothingSelectedDoesNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            var chosen = 0;
            rig.View.SessionChosen += (_, _) => chosen++;
            rig.Model.Open();
            Type(rig, "zebra");

            ViewHarness.Press(rig.Window, Key.Enter, PhysicalKey.Enter);

            chosen.ShouldBe(0);
            rig.Model.IsOpen.ShouldBeTrue();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
