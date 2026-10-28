using System.Text.Json;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Hooks;

/// <summary>The Hooks page: its main-area states, the grouping, the one state of each row, consent with its prompt, inline failures, and the live updates a source reports.</summary>
public sealed class HooksSettingsPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(HooksSettingsPage Page, ScriptedHooksPageSource Source)> ReadyAsync(HooksSnapshot snapshot)
    {
        var source = new ScriptedHooksPageSource { Snapshot = snapshot };
        var page = new HooksSettingsPage(SettingsSection.Plugins, source, TimeZoneInfo.Utc);
        await page.LoadAsync(Token).ConfigureAwait(true);
        return (page, source);
    }

    /// <summary>A page that has not loaded is loading; after the load it lists the hooks grouped by scope in priority order and then by file in run order.</summary>
    [Fact]
    public async Task HooksAreGroupedByScopePriorityThenFile()
    {
        var source = new ScriptedHooksPageSource
        {
            Snapshot = HookItems.Snapshot(
                HookItems.Hook("user-1", scope: HookScopeKind.User, file: "/home/u/.ompos/hooks.json"),
                HookItems.Hook("project-1", scope: HookScopeKind.Project, file: "/work/.ompos/hooks.json"),
                HookItems.Hook("project-2", scope: HookScopeKind.Project, file: "/work/.cursor/hooks.json"),
                HookItems.Hook("managed-1", scope: HookScopeKind.Managed, file: "/etc/ompos/hooks.json"),
                HookItems.Hook("project-3", scope: HookScopeKind.Project, file: "/work/.ompos/hooks.json")),
        };
        var page = new HooksSettingsPage(SettingsSection.Plugins, source);
        page.ContentState.ShouldBe(HooksContentState.Loading);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.ContentState.ShouldBe(HooksContentState.Ready);
        page.Groups.Select(static g => (g.Scope, g.File)).ToArray().ShouldBe(
        [
            (HookScopeKind.Managed, "/etc/ompos/hooks.json"),
            (HookScopeKind.Project, "/work/.ompos/hooks.json"),
            (HookScopeKind.Project, "/work/.cursor/hooks.json"),
            (HookScopeKind.User, "/home/u/.ompos/hooks.json"),
        ]);
        page.Groups[1].Rows.Select(static r => r.Id).ToArray().ShouldBe(["project-1", "project-3"]);
        page.Title.ShouldBe(HooksCopy.Title);
        page.Section.ShouldBe(SettingsSection.Plugins);
    }

    /// <summary>With no hook file the page is empty and offers a valid hooks.json to copy.</summary>
    [Fact]
    public async Task NoHookFilesShowTheEmptyStateWithAValidExample()
    {
        var (page, _) = await ReadyAsync(HookItems.Snapshot()).ConfigureAwait(true);

        page.ContentState.ShouldBe(HooksContentState.Empty);
        using var example = JsonDocument.Parse(page.ExampleJson);
        example.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
        example.RootElement.GetProperty("hooks").GetProperty("beforeShellExecution")[0].GetProperty("command").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Files that exist but are all invalid show the invalid state and their diagnostics; a warning alone does not.</summary>
    [Fact]
    public async Task InvalidFilesShowTheirDiagnostics()
    {
        var error = new HookDiagnosticItem("/work/.cursor/hooks.json", "$.version", "version must be 1", IsError: true);
        var warning = new HookDiagnosticItem("/work/.cursor/hooks.json", "$.extra", "unknown key", IsError: false);

        var (invalid, _) = await ReadyAsync(HookItems.Snapshot() with { Diagnostics = [error, warning] }).ConfigureAwait(true);
        var (warned, _) = await ReadyAsync(HookItems.Snapshot() with { Diagnostics = [warning] }).ConfigureAwait(true);

        invalid.ContentState.ShouldBe(HooksContentState.InvalidConfig);
        invalid.HasDiagnostics.ShouldBeTrue();
        invalid.Diagnostics.ShouldBe([error, warning]);
        error.Summary.ShouldBe("/work/.cursor/hooks.json at $.version: version must be 1");
        warned.ContentState.ShouldBe(HooksContentState.Empty);
    }

    /// <summary>
    /// An untrusted folder alone shows the trust prompt; next to listed hooks it is a banner; trusting calls the source, which then lists the hooks,
    /// and approves nothing.
    /// </summary>
    [Fact]
    public async Task AnUntrustedFolderAsksForTrustAndApprovesNothing()
    {
        var (alone, source) = await ReadyAsync(HookItems.Snapshot() with { WithheldFiles = ["/work/.cursor/hooks.json"] }).ConfigureAwait(true);
        var (banner, _) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("user-1", scope: HookScopeKind.User)) with { WithheldFiles = ["/work/.cursor/hooks.json"] }).ConfigureAwait(true);

        alone.ContentState.ShouldBe(HooksContentState.UntrustedWorkspace);
        alone.WithheldFiles.ShouldBe(["/work/.cursor/hooks.json"]);
        alone.ShowUntrustedBanner.ShouldBeFalse();
        banner.ContentState.ShouldBe(HooksContentState.Ready);
        banner.ShowUntrustedBanner.ShouldBeTrue();

        source.Snapshot = source.Snapshot with { Hooks = [HookItems.Hook("project-1") with { IsConsented = false }] };
        await alone.TrustWorkspaceAsync(Token).ConfigureAwait(true);

        source.Calls.ShouldBe(["Load", "TrustWorkspace", "Load"]);
        alone.ContentState.ShouldBe(HooksContentState.Ready);
        alone.Rows.Single().State.ShouldBe(HookRowState.NeedsConsent);
        alone.WithheldFiles.ShouldBeEmpty();
    }

    /// <summary>With Run hooks off the page says so instead of the list, even when hooks exist; switching it on lists them again through the source.</summary>
    [Fact]
    public async Task RunHooksOffReplacesTheList()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("project-1")) with { RunHooks = false }).ConfigureAwait(true);

        page.ContentState.ShouldBe(HooksContentState.RunHooksOff);
        page.RunHooks.ShouldBeFalse();

        await page.SetRunHooksAsync(true, Token).ConfigureAwait(true);

        source.Calls.ShouldContain("SetRunHooks(True)", StringComparer.Ordinal);
        page.ContentState.ShouldBe(HooksContentState.Ready);
        page.RunHooks.ShouldBeTrue();
    }

    /// <summary>A source that cannot read the hooks puts the page in its error state with the reason.</summary>
    [Fact]
    public async Task AFailedLoadShowsTheError()
    {
        var source = new ScriptedHooksPageSource { LoadFailure = "The hook files could not be read." };
        var page = new HooksSettingsPage(SettingsSection.Plugins, source);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.ContentState.ShouldBe(HooksContentState.Error);
        page.LoadError.ShouldBe("The hook files could not be read.");
    }

    /// <summary>Each row is in exactly one state, and the order of precedence is not applicable, unknown event, quarantined, disabled, needs consent, running, failed, ready.</summary>
    [Fact]
    public async Task EachRowIsInOneStateByPrecedence()
    {
        var tab = HookItems.Hook("tab", "beforeTabFileRead") with { IsApplicable = false, IsEnabled = false };
        var unknown = HookItems.Hook("unknown", "madeUpEvent") with { IsKnownEvent = false, IsEnabled = false };
        var quarantined = HookItems.Hook("quarantined") with { QuarantinedSource = "cursor-project:abc", QuarantineReason = "the shell failed", IsEnabled = false };
        var disabled = HookItems.Hook("disabled") with { IsEnabled = false, IsConsented = false };
        var needs = HookItems.Hook("needs") with { IsConsented = false };
        var running = HookItems.Hook("running");
        var failed = HookItems.Hook("failed") with { LastRun = HookItems.Run("failed", HookRunOutcome.TimedOut) };
        var ready = HookItems.Hook("ready") with { LastRun = HookItems.Run("ready") };
        var (page, source) = await ReadyAsync(HookItems.Snapshot(tab, unknown, quarantined, disabled, needs, running, failed, ready)).ConfigureAwait(true);
        source.RaiseRunning("running");
        page.SetRunning(source.RunningHookIds);

        page.Rows.Select(static r => r.State).ToArray().ShouldBe(
        [
            HookRowState.NotApplicable,
            HookRowState.UnknownEvent,
            HookRowState.Quarantined,
            HookRowState.Disabled,
            HookRowState.NeedsConsent,
            HookRowState.Running,
            HookRowState.LastRunFailed,
            HookRowState.Ready,
        ]);
        page.Rows.Single(static r => string.Equals(r.Id, "failed", StringComparison.Ordinal)).LastRunText.ShouldBe("Timed out in 120 ms");
        page.Rows.Single(static r => string.Equals(r.Id, "needs", StringComparison.Ordinal)).NeedsAttention.ShouldBeTrue();
        page.Rows.Single(static r => string.Equals(r.Id, "tab", StringComparison.Ordinal)).CanToggle.ShouldBeFalse();
        page.Quarantines.Single().ShouldBe(new HookQuarantineRow("cursor-project:abc", "the shell failed", 1));
        page.NeedsConsentCount.ShouldBe(1);
    }

    /// <summary>Selecting a hook opens its consent prompt with the exact command; approving calls the source for that hook, reloads, and announces it; withdrawing does the reverse.</summary>
    [Fact]
    public async Task ApprovingTheSelectedHookRecordsConsentAndAnnouncesIt()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("guard") with { IsConsented = false })).ConfigureAwait(true);

        page.Select("guard");
        var prompt = page.ConsentPrompt.ShouldNotBeNull();
        prompt.ExactText.ShouldBe("./guard.sh");
        prompt.CanApprove.ShouldBeTrue();
        prompt.CanRevoke.ShouldBeFalse();
        page.SelectedRow.ShouldNotBeNull().IsSelected.ShouldBeTrue();

        await page.ApproveSelectedAsync(Token).ConfigureAwait(true);

        source.Calls.ShouldBe(["Load", "Approve(guard)", "Load"]);
        page.Announcement.ShouldBe("Approved guard.");
        page.ConsentPrompt.ShouldNotBeNull().CanApprove.ShouldBeFalse();
        page.ConsentPrompt.CanRevoke.ShouldBeTrue();

        await page.RevokeSelectedAsync(Token).ConfigureAwait(true);

        source.Calls.ShouldContain("Revoke(guard)", StringComparer.Ordinal);
        page.Announcement.ShouldBe("Withdrew approval for guard.");
        page.Rows.Single().State.ShouldBe(HookRowState.NeedsConsent);
    }

    /// <summary>A pre-consented hook cannot be approved or withdrawn, and a hook OmpOS cannot run cannot be approved.</summary>
    [Fact]
    public async Task HooksThatNeedNoConsentOrCannotRunOfferNoApproval()
    {
        var managed = HookItems.Hook("managed", scope: HookScopeKind.Managed) with { IsPreConsented = true };
        var tab = HookItems.Hook("tab", "afterTabFileEdit") with { IsApplicable = false, IsConsented = false };
        var (page, source) = await ReadyAsync(HookItems.Snapshot(managed, tab)).ConfigureAwait(true);

        page.Select("managed");
        page.ConsentPrompt.ShouldNotBeNull().CanApprove.ShouldBeFalse();
        page.ConsentPrompt.CanRevoke.ShouldBeFalse();
        page.Select("tab");
        page.ConsentPrompt.ShouldNotBeNull().CanApprove.ShouldBeFalse();
        await page.ApproveSelectedAsync(Token).ConfigureAwait(true);
        await page.RevokeSelectedAsync(Token).ConfigureAwait(true);

        source.Calls.ShouldBe(["Load"]);
    }

    /// <summary>Trust all approves every hook that needs it, once, and counts them; with none needing it nothing is sent.</summary>
    [Fact]
    public async Task TrustAllApprovesOnlyWhatNeedsConsent()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(
            HookItems.Hook("a") with { IsConsented = false },
            HookItems.Hook("b") with { IsConsented = false },
            HookItems.Hook("c"))).ConfigureAwait(true);
        page.CanTrustAll.ShouldBeTrue();

        await page.ApproveAllAsync(Token).ConfigureAwait(true);

        source.Calls.Count(static c => string.Equals(c, "ApproveAll", StringComparison.Ordinal)).ShouldBe(1);
        page.Announcement.ShouldBe("Approved 2 hooks.");
        page.CanTrustAll.ShouldBeFalse();
        await page.ApproveAllAsync(Token).ConfigureAwait(true);
        source.Calls.Count(static c => string.Equals(c, "ApproveAll", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>Switching a hook off goes through the source and reloads; a hook OmpOS cannot run is never switched.</summary>
    [Fact]
    public async Task SwitchingAHookGoesThroughTheSource()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("guard"), HookItems.Hook("tab", "afterTabFileEdit") with { IsApplicable = false })).ConfigureAwait(true);

        await page.SetEnabledAsync("guard", false, Token).ConfigureAwait(true);
        await page.SetEnabledAsync("tab", true, Token).ConfigureAwait(true);
        await page.SetEnabledAsync("missing", true, Token).ConfigureAwait(true);

        source.Calls.ShouldBe(["Load", "SetEnabled(guard, False)", "Load"]);
        page.Rows[0].State.ShouldBe(HookRowState.Disabled);
        page.Announcement.ShouldBe("guard is now off.");
    }

    /// <summary>A change the source refuses is shown inline, keeps the list as it was and announces nothing.</summary>
    [Fact]
    public async Task AFailedChangeIsShownInlineAndKeepsTheList()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("guard") with { IsConsented = false })).ConfigureAwait(true);
        source.ChangeFailure = "The change could not be stored.";
        page.Select("guard");

        await page.ApproveSelectedAsync(Token).ConfigureAwait(true);

        page.HasError.ShouldBeTrue();
        page.SaveError.ShouldBe("The change could not be stored.");
        page.Announcement.ShouldBeEmpty();
        page.Rows.Single().State.ShouldBe(HookRowState.NeedsConsent);
        page.IsBusy.ShouldBeFalse();
        source.ChangeFailure = null;
        await page.ApproveSelectedAsync(Token).ConfigureAwait(true);
        page.HasError.ShouldBeFalse();
    }

    /// <summary>Lifting a quarantine needs a reason: a blank one is refused inline and sends nothing, and a real one is trimmed and recorded.</summary>
    [Fact]
    public async Task LiftingAQuarantineNeedsAReason()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("guard") with { QuarantinedSource = "cursor-project:abc", QuarantineReason = "the shell failed" })).ConfigureAwait(true);

        await page.LiftQuarantineAsync("cursor-project:abc", "   ", Token).ConfigureAwait(true);
        page.SaveError.ShouldBe(HooksCopy.LiftReasonRequired);
        source.Calls.ShouldBe(["Load"]);

        await page.LiftQuarantineAsync("cursor-project:abc", "  the shell was reinstalled ", Token).ConfigureAwait(true);

        source.Calls.ShouldContain("LiftQuarantine(cursor-project:abc, the shell was reinstalled)", StringComparer.Ordinal);
        page.HasError.ShouldBeFalse();
        page.Quarantines.ShouldBeEmpty();
        page.Rows.Single().State.ShouldBe(HookRowState.Ready);
    }

    /// <summary>While observing, a reported run reaches the log and the last-run text of its hook, the running state follows the source, and after StopObserving nothing changes.</summary>
    [Fact]
    public async Task ObservingFollowsTheSourceUntilItIsStopped()
    {
        var (page, source) = await ReadyAsync(HookItems.Snapshot(HookItems.Hook("guard"))).ConfigureAwait(true);
        page.StartObserving(static action => action());

        source.RaiseRunning("guard");
        page.Rows.Single().State.ShouldBe(HookRowState.Running);
        source.RaiseRunning();
        source.Raise(HookItems.Run("guard", HookRunOutcome.Failed, 42));

        page.RunLog.TotalCount.ShouldBe(1);
        page.Rows.Single().State.ShouldBe(HookRowState.LastRunFailed);
        page.Rows.Single().LastRunText.ShouldBe("Failed in 42 ms");

        page.StopObserving();
        source.Raise(HookItems.Run("guard"));
        page.RunLog.TotalCount.ShouldBe(1);
    }

    /// <summary>The log starts with the source's recent runs.</summary>
    [Fact]
    public async Task TheLogStartsWithTheRecentRuns()
    {
        var source = new ScriptedHooksPageSource { Snapshot = HookItems.Snapshot(HookItems.Hook("guard")) };
        source.AddRun(HookItems.Run("guard"));
        source.AddRun(HookItems.Run("guard", HookRunOutcome.Denied));
        var page = new HooksSettingsPage(SettingsSection.Plugins, source, TimeZoneInfo.Utc);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.RunLog.Rows.Select(static r => r.Run.Outcome).ToArray().ShouldBe([HookRunOutcome.Denied, HookRunOutcome.Succeeded]);
    }
}
