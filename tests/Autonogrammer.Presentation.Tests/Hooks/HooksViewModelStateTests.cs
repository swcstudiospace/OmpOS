using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>
/// The hooks list state machine (31-A): the five states a list can be in are distinct, and enable, disable, move and consent write
/// to the registry and read the list again.
/// </summary>
public sealed class HooksViewModelStateTests
{
    private static readonly HookEntry Format = Hook("format/0", "format", "afterAction", ["dotnet", "format"], consented: true);
    private static readonly HookEntry Lint = Hook("lint/0", "lint", "beforeAction", ["lint", "--strict"], consented: true);
    private static readonly HookEntry Audit = Hook("audit/0", "audit", "beforeAction", ["audit.sh", "two words"], consented: true);

    /// <summary>Loading, empty, error, success and degraded are five different states with their own data.</summary>
    [Fact]
    public async Task LoadingEmptyErrorSuccessDegradedAreDistinct()
    {
        var token = TestContext.Current.CancellationToken;

        var loadingSource = new ScriptedHooksSource(true, Format);
        var loading = new HooksViewModel(loadingSource);
        var pending = loading.LoadAsync(token);
        var loadingState = loading.State;
        loadingState.ShouldBe(HooksViewState.Loading);
        loading.IsBusy.ShouldBeTrue();
        loading.Rows.ShouldBeEmpty();
        loadingSource.CompleteLoad();
        await pending.ConfigureAwait(true);
        loading.IsBusy.ShouldBeFalse();
        loading.State.ShouldBe(HooksViewState.Success);

        var empty = new HooksViewModel(new ScriptedHooksSource());
        await empty.LoadAsync(token).ConfigureAwait(true);

        var error = new HooksViewModel(new ScriptedHooksSource(Format) { LoadFailure = "state file is corrupt" });
        await error.LoadAsync(token).ConfigureAwait(true);

        var success = new HooksViewModel(new ScriptedHooksSource(Format, Lint));
        await success.LoadAsync(token).ConfigureAwait(true);

        var degraded = new HooksViewModel(new ScriptedHooksSource(Format, Hook("new/0", "new", "beforeAction", ["new"], consented: false)));
        await degraded.LoadAsync(token).ConfigureAwait(true);

        new[] { loadingState, empty.State, error.State, success.State, degraded.State }.Distinct().Count().ShouldBe(5);
        empty.State.ShouldBe(HooksViewState.Empty);
        success.State.ShouldBe(HooksViewState.Success);
        degraded.State.ShouldBe(HooksViewState.Degraded);

        empty.Rows.ShouldBeEmpty();
        empty.LoadFailure.ShouldBeEmpty();

        error.State.ShouldBe(HooksViewState.Error);
        error.LoadFailure.ShouldBe("state file is corrupt");
        error.Rows.ShouldBeEmpty();

        success.Rows.Select(row => row.Id).ShouldBe(["format/0", "lint/0"]);
        success.DegradedCount.ShouldBe(0);
        success.Rows.ShouldAllBe(row => !row.IsDegraded && row.DegradedText.Length == 0);

        degraded.DegradedCount.ShouldBe(1);
        degraded.Rows.Single(row => row.IsDegraded).Id.ShouldBe("new/0");
        degraded.Rows.Single(row => row.IsDegraded).BlockedBy.ShouldBe(HookBlockReason.AwaitingConsent);
        degraded.Rows.Single(row => row.IsDegraded).DegradedText.ShouldContain("awaiting consent");
    }

    /// <summary>Move and toggle change the registry first, then the rows are read again in the registry's order.</summary>
    [Fact]
    public async Task MoveAndToggleUpdateRowsAndRegistry()
    {
        var source = new ScriptedHooksSource(Format, Lint, Audit);
        var model = new HooksViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await model.MoveDownAsync("format/0", TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Order.ShouldBe(["lint/0", "format/0", "audit/0"]);
        model.Rows.Select(row => row.Id).ShouldBe(["lint/0", "format/0", "audit/0"]);

        await model.MoveUpAsync("audit/0", TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Order.ShouldBe(["lint/0", "audit/0", "format/0"]);
        model.Rows.Select(row => row.Id).ShouldBe(["lint/0", "audit/0", "format/0"]);

        await model.SetEnabledAsync("audit/0", enabled: false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Entry("audit/0").IsEnabled.ShouldBeFalse();
        var disabled = model.Rows.Single(row => string.Equals(row.Id, "audit/0", StringComparison.Ordinal));
        disabled.IsEnabled.ShouldBeFalse();
        disabled.EnabledText.ShouldBe("Disabled");
        model.Rows.Where(row => row.IsEnabled).Select(row => row.Id).ShouldBe(["lint/0", "format/0"]);

        await model.SetEnabledAsync("audit/0", enabled: true, TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Entry("audit/0").IsEnabled.ShouldBeTrue();
        model.Rows.Single(row => string.Equals(row.Id, "audit/0", StringComparison.Ordinal)).EnabledText.ShouldBe("Enabled");
        model.ActionFailure.ShouldBeEmpty();
        model.IsBusy.ShouldBeFalse();
    }

    /// <summary>A move past either end of the list is not written to the registry.</summary>
    [Fact]
    public async Task MoveBeyondTheEndsOfTheListWritesNothing()
    {
        var source = new ScriptedHooksSource(Format, Lint);
        var model = new HooksViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await model.MoveUpAsync("format/0", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await model.MoveDownAsync("lint/0", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await model.MoveUpAsync("no-such/0", TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.WriteCount.ShouldBe(0);
        source.Order.ShouldBe(["format/0", "lint/0"]);
    }

    /// <summary>Approving records consent for the selected hook only and the list leaves the degraded state once none awaits consent.</summary>
    [Fact]
    public async Task ApproveRecordsConsentForTheSelectedHookOnly()
    {
        var pending = Hook("new/0", "new", "afterAction", ["new"], consented: false);
        var other = Hook("other/0", "other", "afterAction", ["other"], consented: false);
        var source = new ScriptedHooksSource(pending, other);
        var model = new HooksViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.State.ShouldBe(HooksViewState.Degraded);
        model.DegradedCount.ShouldBe(2);

        await model.ApproveSelectedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.WriteCount.ShouldBe(0);

        model.Select("new/0");
        var selected = model.Selected.ShouldNotBeNull();
        selected.Id.ShouldBe("new/0");
        selected.ReviewText.ShouldBe("Reviewing");
        await model.ApproveSelectedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Entry("new/0").IsConsented.ShouldBeTrue();
        source.Entry("other/0").IsConsented.ShouldBeFalse();
        model.State.ShouldBe(HooksViewState.Degraded);
        model.DegradedCount.ShouldBe(1);

        model.Select("other/0");
        await model.ApproveSelectedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(HooksViewState.Success);
        model.DegradedCount.ShouldBe(0);
    }

    /// <summary>A hook of a quarantined plugin is degraded by the quarantine even when its command is consented.</summary>
    [Fact]
    public async Task QuarantineOutranksConsentAndNamesThePlugin()
    {
        var quarantined = Hook("lint/0", "lint", "beforeAction", ["lint"], consented: false) with
        {
            Quarantine = new HookQuarantineEntry("lint", "activation threw"),
        };
        var model = new HooksViewModel(new ScriptedHooksSource(quarantined));

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(HooksViewState.Degraded);
        var row = model.Rows.Single();
        row.BlockedBy.ShouldBe(HookBlockReason.PluginQuarantined);
        row.DegradedText.ShouldBe("Degraded: plugin lint is quarantined (activation threw). This hook will not run.");
    }

    /// <summary>A failed write is reported as the action failure, leaves the registry as it was, and the next action clears it.</summary>
    [Fact]
    public async Task FailedWriteIsReportedAndTheNextActionClearsIt()
    {
        var source = new ScriptedHooksSource(Format, Lint);
        var model = new HooksViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.WriteFailure = "state file is read-only";

        await model.SetEnabledAsync("lint/0", enabled: false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("state file is read-only");
        model.State.ShouldBe(HooksViewState.Success);
        model.Rows.Single(row => string.Equals(row.Id, "lint/0", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue();
        model.IsBusy.ShouldBeFalse();

        source.WriteFailure = null;
        await model.SetEnabledAsync("lint/0", enabled: false, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.ActionFailure.ShouldBeEmpty();
        model.Rows.Single(row => string.Equals(row.Id, "lint/0", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();
    }

    /// <summary>The row shows the exact command, quoting only the arguments that need it, and one numbered line per argument.</summary>
    [Fact]
    public async Task RowShowsTheExactCommandAndNumberedArguments()
    {
        var model = new HooksViewModel(new ScriptedHooksSource(Audit));

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var row = model.Rows.Single();
        row.TriggerText.ShouldBe("Trigger: beforeAction");
        row.CommandText.ShouldBe("Command: audit.sh \"two words\"");
        row.ArgumentListText.ShouldBe("0: audit.sh\n1: \"two words\"");
        row.WorkingDirectoryText.ShouldBe("Working directory: /work");
        row.EnableName.ShouldBe("Enable hook audit/0");
        row.MoveDownName.ShouldBe("Move down hook audit/0");
    }

    /// <summary>Each load, change and action announces its result once through <c>Changed</c> and names the properties that moved.</summary>
    [Fact]
    public async Task EachStepRaisesChangedOnceAfterTheBusyFlagSettles()
    {
        var model = new HooksViewModel(new ScriptedHooksSource(Format, Lint));
        var changes = 0;
        var busyWhenRaised = new List<bool>();
        var names = PropertyChangeRecorder.Record(model);
        model.Changed += (_, _) =>
        {
            changes++;
            busyWhenRaised.Add(model.IsBusy);
        };

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        changes.ShouldBe(2);
        busyWhenRaised.ShouldBe([true, false]);
        names.ShouldAnnounce(nameof(HooksViewModel.State));
        names.ShouldAnnounce(nameof(HooksViewModel.Rows));

        await model.MoveDownAsync("format/0", TestContext.Current.CancellationToken).ConfigureAwait(true);

        changes.ShouldBe(3);
        busyWhenRaised[^1].ShouldBeFalse();
    }

    private static HookEntry Hook(string id, string pluginId, string trigger, string[] arguments, bool consented) =>
        new(id, pluginId, trigger, arguments, "/work", IsEnabled: true, IsConsented: consented, Quarantine: null);
}
