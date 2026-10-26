using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The settings window close protocol and section handling.</summary>
public sealed class SettingsWindowModelTests
{
    /// <summary>Closing with nothing unsaved closes at once.</summary>
    [Fact]
    public void CloseWithoutChangesClosesImmediately()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());

        window.RequestClose().ShouldBe(SettingsCloseOutcome.Closed);

        window.IsClosed.ShouldBeTrue();
        window.IsClosePromptVisible.ShouldBeFalse();
    }

    /// <summary>Closing with unsaved changes stays open and asks first; keeping the window open leaves the draft alone.</summary>
    [Fact]
    public void CloseWithUnsavedChangesAsksFirst()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());
        window.Runtime.SetIdleStop("48");

        window.RequestClose().ShouldBe(SettingsCloseOutcome.ConfirmationRequired);

        window.IsClosed.ShouldBeFalse();
        window.IsClosePromptVisible.ShouldBeTrue();

        window.ResolveClosePrompt(SettingsClosePromptChoice.KeepEditing).ShouldBeFalse();

        window.IsClosed.ShouldBeFalse();
        window.IsClosePromptVisible.ShouldBeFalse();
        window.Runtime.IdleStopText.ShouldBe("48");
    }

    /// <summary>Choosing Save saves the draft, raises the saved event and closes.</summary>
    [Fact]
    public void PromptSaveSavesAndCloses()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());
        RuntimeSettingsValues? saved = null;
        window.RuntimeSettingsSaved += (_, args) => saved = args.Values;
        window.Runtime.SetRetention("30");
        window.RequestClose();

        window.ResolveClosePrompt(SettingsClosePromptChoice.Save).ShouldBeTrue();

        saved.ShouldBe(new RuntimeSettingsValues(24, 30));
        window.Runtime.Saved.RetentionDays.ShouldBe(30);
        window.IsClosed.ShouldBeTrue();
    }

    /// <summary>Choosing Save with a rejected entry keeps the window open, with the inline error still showing.</summary>
    [Fact]
    public void PromptSaveWithARejectedEntryStaysOpen()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());
        window.Runtime.SetIdleStop("0");
        window.RequestClose();

        window.ResolveClosePrompt(SettingsClosePromptChoice.Save).ShouldBeFalse();

        window.IsClosed.ShouldBeFalse();
        window.Runtime.IdleStopError.ShouldNotBeNull();
        window.Runtime.Saved.IdleStopHours.ShouldBe(24);
    }

    /// <summary>Choosing Discard drops the draft and closes without saving.</summary>
    [Fact]
    public void PromptDiscardDropsTheDraftAndCloses()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());
        var raised = false;
        window.RuntimeSettingsSaved += (_, _) => raised = true;
        window.Runtime.SetIdleStop("48");
        window.RequestClose();

        window.ResolveClosePrompt(SettingsClosePromptChoice.Discard).ShouldBeTrue();

        window.IsClosed.ShouldBeTrue();
        window.Runtime.Saved.IdleStopHours.ShouldBe(24);
        raised.ShouldBeFalse();
    }

    /// <summary>Answering a prompt that is not showing is an error.</summary>
    [Fact]
    public void AnsweringWithoutAPromptThrows()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());

        Should.Throw<InvalidOperationException>(() => window.ResolveClosePrompt(SettingsClosePromptChoice.Discard));
    }

    /// <summary>Resetting the Runtime section restores its defaults; a section without runtime settings reports no reset.</summary>
    [Fact]
    public void ResetSectionResetsOnlyTheRuntimeSection()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel(new RuntimeSettingsValues(48, 30)));

        window.ResetSection(SettingsSection.ModelProviders).ShouldBeFalse();
        window.Runtime.IdleStopText.ShouldBe("48");

        window.ResetSection(SettingsSection.Runtime).ShouldBeTrue();
        window.Runtime.Draft.ShouldBe(RuntimeSettingsValues.Defaults);
        window.HasUnsavedChanges.ShouldBeTrue();
    }

    /// <summary>Selecting a section changes the selection and rejects an undefined value.</summary>
    [Fact]
    public void SelectSectionChangesTheSelection()
    {
        var window = new SettingsWindowModel(new RuntimeSettingsModel());

        window.SelectSection(SettingsSection.ModelProviders);

        window.SelectedSection.ShouldBe(SettingsSection.ModelProviders);
        Should.Throw<ArgumentOutOfRangeException>(() => window.SelectSection((SettingsSection)99));
    }
}
