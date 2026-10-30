using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Closing the Settings window: Esc, Ctrl+W, the Close button and the close box, and the unsaved-changes prompt they open.</summary>
public sealed class SettingsClosePromptTests
{
    private static void Press(SettingsFixture fixture, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None) =>
        SettingsTree.Press(fixture.Window, key, physical, modifiers);

    /// <summary>Makes the Runtime page dirty through its entry, with focus in the entry, the way a user does.</summary>
    private static RuntimePageView EditRuntime(SettingsFixture fixture)
    {
        var view = (RuntimePageView)fixture.Show(SettingsSection.Runtime);
        view.IdleStop.Focus();
        view.IdleStop.Text = "48";
        fixture.Settle();
        return view;
    }

    /// <summary>With nothing unsaved, Esc, Ctrl+W and the Close button each close the window with no prompt.</summary>
    [Fact]
    public async Task EscCtrlWAndTheCloseButtonCloseACleanWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            foreach (var close in new Action<SettingsFixture>[]
            {
                static f => Press(f, Key.Escape, PhysicalKey.Escape),
                static f => Press(f, Key.W, PhysicalKey.W, RawInputModifiers.Control),
                static f => SettingsTree.Click(f.Window, f.Window.CloseWindow),
            })
            {
                using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
                var closed = false;
                fixture.Window.Closed += (_, _) => closed = true;

                close(fixture);
                fixture.Settle();

                fixture.Model.IsClosePromptVisible.ShouldBeFalse();
                fixture.Model.IsClosed.ShouldBeTrue();
                closed.ShouldBeTrue("the window closes once the model says it is closed");
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>With unsaved changes Esc opens the prompt instead of closing; focus moves into it, and the page stays as it was.</summary>
    [Fact]
    public async Task EscWithUnsavedChangesOpensThePromptAndMovesFocusIntoIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var closed = false;
            window.Closed += (_, _) => closed = true;
            _ = EditRuntime(fixture);

            Press(fixture, Key.Escape, PhysicalKey.Escape);

            fixture.Model.IsClosePromptVisible.ShouldBeTrue();
            fixture.Model.IsClosed.ShouldBeFalse();
            closed.ShouldBeFalse();
            window.Prompt.IsVisible.ShouldBeTrue();
            SettingsTree.FocusedName(window).ShouldBe(SettingsCopy.ClosePromptSaveLabel, "focus moves into the prompt");
            SettingsTree.Texts(window.Prompt).ShouldContain(SettingsCopy.ClosePromptHeading, StringComparer.Ordinal);
            SettingsTree.Texts(window.Prompt).ShouldContain(SettingsCopy.ClosePromptBody, StringComparer.Ordinal);
            window.PromptSave.Content.ShouldBe(SettingsCopy.ClosePromptSaveLabel);
            window.PromptDiscard.Content.ShouldBe(SettingsCopy.ClosePromptDiscardLabel);
            window.PromptKeep.Content.ShouldBe(SettingsCopy.ClosePromptKeepEditingLabel);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Keep editing, by its button or by Esc, hides the prompt, keeps the window and the edit, and gives focus back to where it was.</summary>
    [Fact]
    public async Task KeepEditingDismissesThePromptAndReturnsFocus()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var view = EditRuntime(fixture);

            Press(fixture, Key.Escape, PhysicalKey.Escape);
            SettingsTree.Click(window, window.PromptKeep);
            fixture.Settle();

            fixture.Model.IsClosePromptVisible.ShouldBeFalse();
            fixture.Model.IsClosed.ShouldBeFalse();
            window.Prompt.IsVisible.ShouldBeFalse();
            view.IdleStop.Text.ShouldBe("48");
            view.IdleStop.IsFocused.ShouldBeTrue("focus returns to the entry that had it");

            Press(fixture, Key.Escape, PhysicalKey.Escape);
            fixture.Model.IsClosePromptVisible.ShouldBeTrue();
            Press(fixture, Key.Escape, PhysicalKey.Escape);

            fixture.Model.IsClosePromptVisible.ShouldBeFalse("Esc with the prompt showing keeps editing");
            fixture.Model.IsClosed.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Discard and close drops every edit and closes the window without writing anything.</summary>
    [Fact]
    public async Task DiscardAndCloseDropsTheEditsAndClosesTheWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var closed = false;
            window.Closed += (_, _) => closed = true;
            _ = EditRuntime(fixture);
            fixture.General.SetMotion(MotionPreference.Reduced);

            Press(fixture, Key.Escape, PhysicalKey.Escape);
            SettingsTree.Click(window, window.PromptDiscard);
            fixture.Settle();

            closed.ShouldBeTrue();
            fixture.Model.IsClosed.ShouldBeTrue();
            fixture.Model.HasUnsavedChanges.ShouldBeFalse();
            fixture.RuntimeSaved.ShouldBeEmpty();
            fixture.GeneralSource.Saved.ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Save and close writes every dirty page through its own source and then closes.</summary>
    [Fact]
    public async Task SaveAndCloseSavesEveryDirtyPageAndCloses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var closed = false;
            window.Closed += (_, _) => closed = true;
            _ = EditRuntime(fixture);
            fixture.General.SetMotion(MotionPreference.Reduced);
            fixture.Agents.SetTerminalAutoRun(true);

            Press(fixture, Key.Escape, PhysicalKey.Escape);
            SettingsTree.Click(window, window.PromptSave);
            fixture.Settle();

            closed.ShouldBeTrue();
            fixture.RuntimeSaved.ShouldHaveSingleItem().IdleStopHours.ShouldBe(48);
            fixture.GeneralSource.Saved.ShouldHaveSingleItem().Motion.ShouldBe(MotionPreference.Reduced);
            fixture.AgentsSource.Saved.ShouldHaveSingleItem().TerminalAutoRun.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A page that rejects its save keeps the window open on that page with its error showing; the other dirty pages are still saved.</summary>
    [Fact]
    public async Task ARejectedSaveKeepsTheWindowOpenOnThePageThatRefusedIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.GeneralSource.SaveFailure = "The settings file is read-only.").ConfigureAwait(true);
            var window = fixture.Window;
            var closed = false;
            window.Closed += (_, _) => closed = true;
            _ = EditRuntime(fixture);
            fixture.General.SetMotion(MotionPreference.Full);

            Press(fixture, Key.Escape, PhysicalKey.Escape);
            SettingsTree.Click(window, window.PromptSave);
            fixture.Settle();

            closed.ShouldBeFalse("a rejected save must not lose the user's edit by closing");
            fixture.Model.IsClosed.ShouldBeFalse();
            fixture.Model.IsClosePromptVisible.ShouldBeFalse();
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.General);
            window.CurrentView.ShouldBeOfType<GeneralPageView>();
            SettingsTree.Texts(window.CurrentView!).ShouldContain("The settings file is read-only.", StringComparer.Ordinal);
            fixture.RuntimeSaved.ShouldHaveSingleItem("the other dirty page was still saved");
            fixture.General.IsDirty.ShouldBeTrue();
            window.Footer.Text.ShouldBe(SettingsCopy.UnsavedBadge);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The window's own close box asks like Esc does: a clean window closes, an edited one shows the prompt and stays open.</summary>
    [Fact]
    public async Task TheCloseBoxAsksTheModelAndNeverClosesOverUnsavedChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var closed = false;
            window.Closed += (_, _) => closed = true;
            _ = EditRuntime(fixture);

            window.Close();
            fixture.Settle();

            closed.ShouldBeFalse();
            fixture.Model.IsClosePromptVisible.ShouldBeTrue();

            SettingsTree.Click(window, window.PromptDiscard);
            fixture.Settle();

            closed.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }
}
