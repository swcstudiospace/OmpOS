using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The settings window protocol across several pages: selection, dirty tracking, save, reset and the close prompt.</summary>
public sealed class SettingsWindowModelTests
{
    /// <summary>The window refuses a missing, empty, repeated or null page list.</summary>
    [Fact]
    public void ConstructionRefusesAnInvalidPageList()
    {
        Should.Throw<ArgumentNullException>(() => new SettingsWindowModel(null!));
        Should.Throw<ArgumentException>(() => new SettingsWindowModel([]));
        Should.Throw<ArgumentException>(() => new SettingsWindowModel([null!]));
        Should.Throw<ArgumentException>(() => new SettingsWindowModel(
            [new ScriptedSettingsPage(SettingsSection.General), new ScriptedSettingsPage(SettingsSection.General)]));
    }

    /// <summary>The pages keep the navigation order they were given and the first one is selected.</summary>
    [Fact]
    public void PagesKeepTheirOrderAndTheFirstIsSelected()
    {
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var runtime = new ScriptedSettingsPage(SettingsSection.Runtime);
        var window = new SettingsWindowModel([general, runtime]);

        window.Pages.Count.ShouldBe(2);
        window.Pages[0].ShouldBeSameAs(general);
        window.Pages[1].ShouldBeSameAs(runtime);
        window.SelectedSection.ShouldBe(SettingsSection.General);
        window.SelectedPage.ShouldBeSameAs(general);
    }

    /// <summary>Selecting a section changes the selection and announces it; an unknown or absent section is refused.</summary>
    [Fact]
    public void SelectSectionChangesTheSelectionAndRejectsUnknownSections()
    {
        var runtime = new ScriptedSettingsPage(SettingsSection.Runtime);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents);
        var window = new SettingsWindowModel([runtime, agents]);
        var announced = PropertyChangeRecorder.Record(window);

        window.SelectSection(SettingsSection.Agents);

        window.SelectedSection.ShouldBe(SettingsSection.Agents);
        window.SelectedPage.ShouldBeSameAs(agents);
        announced.ShouldAnnounce(nameof(SettingsWindowModel.SelectedSection));
        announced.ShouldAnnounce(nameof(SettingsWindowModel.SelectedPage));
        Should.Throw<ArgumentOutOfRangeException>(() => window.SelectSection((SettingsSection)99));
        Should.Throw<ArgumentOutOfRangeException>(() => window.SelectSection(SettingsSection.Plugins));
        window.SelectedSection.ShouldBe(SettingsSection.Agents);
    }

    /// <summary>Closing with nothing unsaved closes at once.</summary>
    [Fact]
    public void CloseWithoutChangesClosesImmediately()
    {
        var window = new SettingsWindowModel([new ScriptedSettingsPage(SettingsSection.Runtime)]);

        window.RequestClose().ShouldBe(SettingsCloseOutcome.Closed);

        window.IsClosed.ShouldBeTrue();
        window.IsClosePromptVisible.ShouldBeFalse();
    }

    /// <summary>Closing with unsaved changes stays open and asks first; keeping the window open leaves the draft alone.</summary>
    [Fact]
    public async Task CloseWithUnsavedChangesAsksFirst()
    {
        var runtime = new RuntimePage(new RuntimeSettingsModel());
        var window = new SettingsWindowModel([runtime]);
        runtime.Model.SetIdleStop("48");

        window.HasUnsavedChanges.ShouldBeTrue();
        window.RequestClose().ShouldBe(SettingsCloseOutcome.ConfirmationRequired);

        window.IsClosed.ShouldBeFalse();
        window.IsClosePromptVisible.ShouldBeTrue();

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.KeepEditing, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeFalse();

        window.IsClosed.ShouldBeFalse();
        window.IsClosePromptVisible.ShouldBeFalse();
        runtime.Model.IdleStopText.ShouldBe("48");
    }

    /// <summary>Choosing Save saves every dirty page, leaves a clean page alone, raises the Runtime saved event and closes.</summary>
    [Fact]
    public async Task PromptSaveSavesEveryDirtyPageAndCloses()
    {
        var runtime = new RuntimePage(new RuntimeSettingsModel());
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents);
        var tab = new ScriptedSettingsPage(SettingsSection.Tab);
        var window = new SettingsWindowModel([runtime, general, agents, tab]);
        RuntimeSettingsValues? saved = null;
        runtime.Saved += (_, args) => saved = args.Values;
        runtime.Model.SetRetention("30");
        general.SetDirty(true);
        agents.SetDirty(true);
        window.RequestClose();

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.Save, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeTrue();

        saved.ShouldBe(new RuntimeSettingsValues(24, 30));
        runtime.Model.Saved.RetentionDays.ShouldBe(30);
        general.SaveCalls.ShouldBe(1);
        agents.SaveCalls.ShouldBe(1);
        tab.SaveCalls.ShouldBe(0);
        window.IsClosed.ShouldBeTrue();
        window.HasUnsavedChanges.ShouldBeFalse();
    }

    /// <summary>
    /// Choosing Save with a page that rejects its save keeps the window open on that page with its error showing; the other dirty
    /// pages were still saved.
    /// </summary>
    [Fact]
    public async Task PromptSaveWithARejectedPageStaysOpenOnThatPage()
    {
        var runtime = new RuntimePage(new RuntimeSettingsModel());
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents) { RejectSave = true };
        var window = new SettingsWindowModel([runtime, general, agents]);
        runtime.Model.SetIdleStop("0");
        general.SetDirty(true);
        agents.SetDirty(true);
        window.RequestClose();

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.Save, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeFalse();

        window.IsClosed.ShouldBeFalse();
        window.IsClosePromptVisible.ShouldBeFalse();
        window.SelectedSection.ShouldBe(SettingsSection.Runtime);
        runtime.Model.IdleStopError.ShouldNotBeNull();
        runtime.Model.Saved.IdleStopHours.ShouldBe(24);
        general.SaveCalls.ShouldBe(1);
        agents.SaveCalls.ShouldBe(1);
        agents.HasError.ShouldBeTrue();
        window.HasUnsavedChanges.ShouldBeTrue();
    }

    /// <summary>A rejection by a later page selects that page when no earlier page rejected.</summary>
    [Fact]
    public async Task PromptSaveSelectsTheFirstRejectingPage()
    {
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents) { RejectSave = true };
        var tab = new ScriptedSettingsPage(SettingsSection.Tab) { RejectSave = true };
        var window = new SettingsWindowModel([general, agents, tab]);
        general.SetDirty(true);
        agents.SetDirty(true);
        tab.SetDirty(true);
        window.RequestClose();

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.Save, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeFalse();

        window.SelectedSection.ShouldBe(SettingsSection.Agents);
    }

    /// <summary>Choosing Discard drops every draft and closes without saving.</summary>
    [Fact]
    public async Task PromptDiscardDropsEveryDraftAndCloses()
    {
        var runtime = new RuntimePage(new RuntimeSettingsModel());
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var window = new SettingsWindowModel([runtime, general]);
        var raised = false;
        runtime.Saved += (_, _) => raised = true;
        runtime.Model.SetIdleStop("48");
        general.SetDirty(true);
        window.RequestClose();

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.Discard, TestContext.Current.CancellationToken).ConfigureAwait(true))
            .ShouldBeTrue();

        window.IsClosed.ShouldBeTrue();
        runtime.Model.Saved.IdleStopHours.ShouldBe(24);
        runtime.Model.IdleStopText.ShouldBe("24");
        general.DiscardCalls.ShouldBe(1);
        general.SaveCalls.ShouldBe(0);
        raised.ShouldBeFalse();
    }

    /// <summary>Answering a prompt that is not showing, or with an undefined choice, is an error.</summary>
    [Fact]
    public void AnsweringWithoutAPromptOrWithAnUndefinedChoiceThrows()
    {
        var window = new SettingsWindowModel([new ScriptedSettingsPage(SettingsSection.Runtime)]);
        var token = TestContext.Current.CancellationToken;

        Should.Throw<InvalidOperationException>(() => { _ = window.ResolveClosePromptAsync(SettingsClosePromptChoice.Discard, token); });
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = window.ResolveClosePromptAsync((SettingsClosePromptChoice)9, token); });
    }

    /// <summary>Resetting one section resets only its page; the window reports the unsaved change.</summary>
    [Fact]
    public void ResetSectionResetsOnlyThatPage()
    {
        var runtime = new RuntimePage(new RuntimeSettingsModel(new RuntimeSettingsValues(48, 30)));
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var window = new SettingsWindowModel([runtime, general]);

        window.ResetSection(SettingsSection.General);
        general.ResetCalls.ShouldBe(1);
        runtime.Model.IdleStopText.ShouldBe("48");

        window.ResetSection(SettingsSection.Runtime);
        runtime.Model.Draft.ShouldBe(RuntimeSettingsValues.Defaults);
        window.HasUnsavedChanges.ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => window.ResetSection(SettingsSection.Tab));
    }

    /// <summary>Saving one section saves only that page and reports whether it was accepted.</summary>
    [Fact]
    public async Task SaveSectionSavesOnlyThatPage()
    {
        var token = TestContext.Current.CancellationToken;
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents) { RejectSave = true };
        var window = new SettingsWindowModel([general, agents]);
        general.SetDirty(true);
        agents.SetDirty(true);

        (await window.SaveAsync(SettingsSection.General, token).ConfigureAwait(true)).ShouldBeTrue();
        (await window.SaveAsync(SettingsSection.Agents, token).ConfigureAwait(true)).ShouldBeFalse();

        general.SaveCalls.ShouldBe(1);
        agents.SaveCalls.ShouldBe(1);
        window.HasUnsavedChanges.ShouldBeTrue();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => window.SaveAsync(SettingsSection.Tab, token)).ConfigureAwait(true);
    }

    /// <summary>The window announces <c>HasUnsavedChanges</c> when a page's dirty flag changes, and reports any dirty page.</summary>
    [Fact]
    public void HasUnsavedChangesFollowsEveryPage()
    {
        var general = new ScriptedSettingsPage(SettingsSection.General);
        var agents = new ScriptedSettingsPage(SettingsSection.Agents);
        var window = new SettingsWindowModel([general, agents]);
        var announced = PropertyChangeRecorder.Record(window);

        window.HasUnsavedChanges.ShouldBeFalse();
        agents.SetDirty(true);

        window.HasUnsavedChanges.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(SettingsWindowModel.HasUnsavedChanges));

        agents.SetDirty(false);
        window.HasUnsavedChanges.ShouldBeFalse();
    }

    /// <summary>Loading every page reaches each page once; a page that fails to load shows its error and leaves the others ready.</summary>
    [Fact]
    public async Task LoadAllLoadsEveryPageAndAFailureStaysOnItsPage()
    {
        var general = new GeneralPage(new ScriptedGeneralSource(new GeneralSettingsValues(MotionPreference.Reduced, ThemePreference.Dark)));
        var agents = new AgentsPage(new ScriptedAgentsSource { LoadFailure = "terminal settings are unreadable" });
        var scripted = new ScriptedSettingsPage(SettingsSection.Tab);
        var window = new SettingsWindowModel([general, agents, scripted]);

        await window.LoadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        general.State.ShouldBe(SettingsPageState.Ready);
        general.Motion.ShouldBe(MotionPreference.Reduced);
        agents.State.ShouldBe(SettingsPageState.Error);
        agents.LoadError.ShouldBe("terminal settings are unreadable");
        scripted.LoadCalls.ShouldBe(1);
        window.HasUnsavedChanges.ShouldBeFalse();
    }
}
