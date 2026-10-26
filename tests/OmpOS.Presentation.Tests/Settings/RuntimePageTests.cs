using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Runtime page over the Runtime model: dirty tracking, save, reset, discard and inline rejection.</summary>
public sealed class RuntimePageTests
{
    /// <summary>The page fills the Runtime section, is ready at once and needs no load.</summary>
    [Fact]
    public async Task PageIsReadyAtOnce()
    {
        var page = new RuntimePage(new RuntimeSettingsModel());

        page.Section.ShouldBe(SettingsSection.Runtime);
        page.Title.ShouldBe(SettingsCopy.RuntimeTitle);
        page.State.ShouldBe(SettingsPageState.Ready);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
    }

    /// <summary>Editing a draft entry makes the page dirty and announces it; saving raises the saved event once and clears it.</summary>
    [Fact]
    public async Task SaveRaisesTheSavedEventAndClearsTheDirtyFlag()
    {
        var page = new RuntimePage(new RuntimeSettingsModel());
        var announced = PropertyChangeRecorder.Record(page);
        var raised = new List<RuntimeSettingsValues>();
        page.Saved += (_, args) => raised.Add(args.Values);

        page.Model.SetIdleStop("48");

        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(ISettingsPage.IsDirty));

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        raised.Count.ShouldBe(1);
        raised[0].ShouldBe(new RuntimeSettingsValues(48, 7));
        page.IsDirty.ShouldBeFalse();
        page.Model.Saved.IdleStopHours.ShouldBe(48);
    }

    /// <summary>Saving a clean page succeeds and raises nothing.</summary>
    [Fact]
    public async Task SavingACleanPageDoesNothing()
    {
        var page = new RuntimePage(new RuntimeSettingsModel());
        var raised = false;
        page.Saved += (_, _) => raised = true;

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        raised.ShouldBeFalse();
    }

    /// <summary>A rejected entry shows its error inline, can never be saved, and the saved values stay.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("721")]
    [InlineData("")]
    [InlineData("soon")]
    public async Task ARejectedEntryCannotBeSaved(string entry)
    {
        var page = new RuntimePage(new RuntimeSettingsModel());
        var raised = false;
        page.Saved += (_, _) => raised = true;
        page.Model.SetIdleStop(entry);

        (await page.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        page.HasError.ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
        page.Model.IdleStopError.ShouldNotBeNull();
        page.Model.Saved.IdleStopHours.ShouldBe(24);
        raised.ShouldBeFalse();
    }

    /// <summary>Reset returns the draft to the defaults without saving; discard returns it to the saved values and clears errors.</summary>
    [Fact]
    public void ResetAndDiscardMoveTheDraft()
    {
        var page = new RuntimePage(new RuntimeSettingsModel(new RuntimeSettingsValues(48, 30)));

        page.Reset();

        page.Model.Draft.ShouldBe(RuntimeSettingsValues.Defaults);
        page.Model.Saved.ShouldBe(new RuntimeSettingsValues(48, 30));
        page.IsDirty.ShouldBeTrue();

        page.Model.SetRetention("nope");
        page.HasError.ShouldBeTrue();

        page.Discard();

        page.Model.Draft.ShouldBe(new RuntimeSettingsValues(48, 30));
        page.HasError.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A page without a model is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingModel() =>
        Should.Throw<ArgumentNullException>(() => new RuntimePage(null!));
}
