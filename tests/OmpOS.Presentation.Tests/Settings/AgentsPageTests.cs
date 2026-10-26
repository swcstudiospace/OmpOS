using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Agents page: the terminal auto-run switch is off by default and a change is a save, not instant.</summary>
public sealed class AgentsPageTests
{
    /// <summary>Auto-run is off by default and the approval policy note is part of the page.</summary>
    [Fact]
    public async Task AutoRunIsOffByDefault()
    {
        var page = new AgentsPage(new ScriptedAgentsSource());

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.TerminalAutoRun.ShouldBeFalse();
        page.IsAutoRunWarningVisible.ShouldBeFalse();
        SettingsCopy.ApprovalPolicyNote.ShouldContain("omp's approval policy", Case.Sensitive);
    }

    /// <summary>Switching auto-run on is a draft that shows the warning; the source is untouched until Save.</summary>
    [Fact]
    public async Task ChangingTheSwitchIsASaveNotInstant()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedAgentsSource();
        var page = new AgentsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(page);

        page.SetTerminalAutoRun(true);

        page.TerminalAutoRun.ShouldBeTrue();
        page.IsAutoRunWarningVisible.ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
        announced.ShouldAnnounce(nameof(AgentsPage.TerminalAutoRun));
        source.SaveCalls.ShouldBe(0);
        source.Values.TerminalAutoRun.ShouldBeFalse();

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.SaveCalls.ShouldBe(1);
        source.Values.TerminalAutoRun.ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Switching on and off again is not a change.</summary>
    [Fact]
    public async Task SwitchingBackIsNotDirty()
    {
        var page = new AgentsPage(new ScriptedAgentsSource());
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.SetTerminalAutoRun(true);
        page.SetTerminalAutoRun(false);

        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A rejected write shows inline, keeps the old saved value and the draft.</summary>
    [Fact]
    public async Task ARejectedWriteKeepsTheDraftAndShowsTheReason()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedAgentsSource { SaveFailure = "terminal settings file is locked" };
        var page = new AgentsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        page.SetTerminalAutoRun(true);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        page.SaveError.ShouldBe("terminal settings file is locked");
        page.HasError.ShouldBeTrue();
        page.IsDirty.ShouldBeTrue();
        page.Saved.TerminalAutoRun.ShouldBeFalse();
        source.Values.TerminalAutoRun.ShouldBeFalse();
    }

    /// <summary>Reset returns to off (the default) and discard returns to the saved value.</summary>
    [Fact]
    public async Task ResetAndDiscardMoveTheDraft()
    {
        var source = new ScriptedAgentsSource();
        await source.SaveAsync(new AgentsSettingsValues(TerminalAutoRun: true), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var page = new AgentsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        page.TerminalAutoRun.ShouldBeTrue();

        page.Reset();

        page.TerminalAutoRun.ShouldBeFalse();
        page.IsDirty.ShouldBeTrue();

        page.Discard();

        page.TerminalAutoRun.ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A load failure becomes the error state and the switch cannot be edited.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var page = new AgentsPage(new ScriptedAgentsSource { LoadFailure = "terminal settings are unreadable" });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        page.SetTerminalAutoRun(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("terminal settings are unreadable");
        page.TerminalAutoRun.ShouldBeFalse();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new AgentsPage(null!));
}
