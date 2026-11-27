using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Agents page: the terminal auto-run switch was removed (Phase 81), so the page loads and saves nothing editable.</summary>
public sealed class AgentsPageTests
{
    /// <summary>The page loads ready, carries the removal announcement and the approval policy note.</summary>
    [Fact]
    public async Task PageLoadsReadyWithTheRemovalAnnouncement()
    {
        var page = new AgentsPage(new ScriptedAgentsSource());

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsDirty.ShouldBeFalse();
        SettingsCopy.AutoRunRemovedNote.ShouldContain("Terminal auto-run was removed", Case.Sensitive);
        SettingsCopy.ApprovalPolicyNote.ShouldContain("Every terminal command an agent proposes waits for your approval", Case.Sensitive);
    }

    /// <summary>There is no editable value, so saving never writes to the source.</summary>
    [Fact]
    public async Task SavingNeverWritesToTheSource()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedAgentsSource();
        var page = new AgentsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);

        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.SaveCalls.ShouldBe(0);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Reset and discard move nothing because there is no draft value.</summary>
    [Fact]
    public async Task ResetAndDiscardAreNoOps()
    {
        var page = new AgentsPage(new ScriptedAgentsSource());
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Reset();
        page.Discard();

        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A load failure becomes the error state.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var page = new AgentsPage(new ScriptedAgentsSource { LoadFailure = "terminal settings are unreadable" });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("terminal settings are unreadable");
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new AgentsPage(null!));
}
