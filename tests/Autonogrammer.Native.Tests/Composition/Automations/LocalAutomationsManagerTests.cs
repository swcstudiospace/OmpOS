using Autonogrammer.Harness.Automations.Tests;
using Autonogrammer.Native.Composition.Automations;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;
using ViewDraft = Autonogrammer.Presentation.Automations.AutomationDraft;

namespace Autonogrammer.Native.Tests.Composition.Automations;

/// <summary>The desktop manager of the Automations page: what it saves, what it refuses, and what it reads back, over the real file store in a temporary folder.</summary>
public sealed class LocalAutomationsManagerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ViewDraft Draft(params AutomationTriggerDraft[] triggers) => new(
        "Morning summary",
        AutomationFixtures.Prompt,
        Enabled: true,
        "UTC",
        triggers,
        "local",
        [],
        [],
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        MaxConcurrentRuns: 1,
        MaxRunsPerHour: 12,
        MaxRuntimeMinutes: 30);

    private static AutomationTriggerDraft Cron(string expression) => new("cron", expression, string.Empty, [], [], []);

    private static AutomationsHost Host(TempTree tree, FakeTimeProvider clock) =>
        new(Path.Combine(tree.Root, "automations.json"), Path.Combine(tree.Root, "consent.json"), cloud: null, () => tree.Workspace, localAvailable: true, clock);

    /// <summary>The desktop receives no webhook: a webhook trigger is refused with the sentence that says where to create it, the editor is told to offer the schedule only, and no secret is ever returned.</summary>
    [Fact]
    public async Task AWebhookTriggerIsRefusedAndTheEditorOffersTheScheduleOnly()
    {
        using var tree = new TempTree();
        var host = Host(tree, new FakeTimeProvider(Start));
        await using var hostScope = host.ConfigureAwait(true);
        var manager = host.Manager;

        manager.SupportsWebhooks.ShouldBeFalse();
        var webhook = new AutomationTriggerDraft("webhook", string.Empty, "generic", [], [], []);
        var refused = await Should.ThrowAsync<AutomationsSourceException>(() => manager.SaveAsync(null, Draft(Cron("0 9 * * *"), webhook), Token)).ConfigureAwait(true);
        var rotation = await Should.ThrowAsync<AutomationsSourceException>(() => manager.RotateWebhookSecretAsync("auto-1", Token)).ConfigureAwait(true);

        refused.Message.ShouldBe(AutomationsCopy.WebhookNotSupported);
        rotation.Message.ShouldBe(AutomationsCopy.WebhookNotSupported);
        (await manager.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty("nothing was saved");
        var editor = new AutomationEditorModel(manager, existing: null);
        editor.SupportsWebhooks.ShouldBeFalse();
        editor.ScheduleOnlyNote.ShouldBe(AutomationsCopy.ScheduleOnlyNote);
    }

    /// <summary>A saved automation comes back as it was saved, with the next run computed in its time zone, and a webhook path and secret that are always empty.</summary>
    [Fact]
    public async Task ASavedAutomationComesBackWithItsNextRun()
    {
        using var tree = new TempTree();
        var host = Host(tree, new FakeTimeProvider(Start));
        await using var hostScope = host.ConfigureAwait(true);

        var saved = await host.Manager.SaveAsync(null, Draft(Cron("30 9 * * *")), Token).ConfigureAwait(true);
        var listed = (await host.Manager.ListAsync(Token).ConfigureAwait(true)).ShouldHaveSingleItem();

        saved.WebhookSecret.ShouldBeNull();
        listed.Id.ShouldBe(saved.Entry.Id);
        listed.Draft.Name.ShouldBe("Morning summary");
        listed.Draft.TargetKind.ShouldBe("local");
        listed.Draft.Triggers.ShouldHaveSingleItem().Cron.ShouldBe("30 9 * * *");
        listed.NextRunAt.ShouldBe(Start.AddMinutes(30));
        listed.WebhookPath.ShouldBeEmpty();
        listed.HasWebhookSecret.ShouldBeFalse();
    }

    /// <summary>An automation without a schedule or with an invalid one is refused with the harness's own sentence, and editing replaces the saved one instead of adding another.</summary>
    [Fact]
    public async Task InvalidDraftsAreRefusedAndAnEditReplacesTheSavedAutomation()
    {
        using var tree = new TempTree();
        var host = Host(tree, new FakeTimeProvider(Start));
        await using var hostScope = host.ConfigureAwait(true);
        var manager = host.Manager;

        var refused = await Should.ThrowAsync<AutomationsSourceException>(() => manager.SaveAsync(null, Draft(Cron("not a schedule")), Token)).ConfigureAwait(true);
        var saved = await manager.SaveAsync(null, Draft(Cron("0 9 * * *")), Token).ConfigureAwait(true);
        var edited = await manager.SaveAsync(saved.Entry.Id, Draft(Cron("0 10 * * *")) with { Name = "Renamed" }, Token).ConfigureAwait(true);

        refused.Message.ShouldNotBeEmpty();
        edited.Entry.Id.ShouldBe(saved.Entry.Id);
        var listed = (await manager.ListAsync(Token).ConfigureAwait(true)).ShouldHaveSingleItem();
        listed.Draft.Name.ShouldBe("Renamed");
    }

    /// <summary>Turning an automation off and deleting it reach the file; the schedule preview reports the next runs in the zone or a sentence.</summary>
    [Fact]
    public async Task ToggleDeleteAndPreviewWork()
    {
        using var tree = new TempTree();
        var host = Host(tree, new FakeTimeProvider(Start));
        await using var hostScope = host.ConfigureAwait(true);
        var manager = host.Manager;
        var saved = await manager.SaveAsync(null, Draft(Cron("0 9 * * *")), Token).ConfigureAwait(true);

        await manager.SetEnabledAsync(saved.Entry.Id, false, Token).ConfigureAwait(true);
        var off = (await manager.ListAsync(Token).ConfigureAwait(true)).Single();
        var preview = await manager.PreviewCronAsync("0 9 * * *", "UTC", 3, Token).ConfigureAwait(true);
        var bad = await manager.PreviewCronAsync("61 * * * *", "UTC", 3, Token).ConfigureAwait(true);
        var zone = await manager.PreviewCronAsync("0 9 * * *", "Not/AZone", 3, Token).ConfigureAwait(true);
        await manager.DeleteAsync(saved.Entry.Id, Token).ConfigureAwait(true);

        off.Draft.Enabled.ShouldBeFalse();
        off.NextRunAt.ShouldBeNull();
        preview.NextRuns.ToArray().ShouldBe([Start.AddDays(1), Start.AddDays(2), Start.AddDays(3)]);
        bad.Error.ShouldNotBeEmpty();
        zone.Error.ShouldNotBeEmpty();
        (await manager.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Run now starts the run through the runner (a second one while it runs is refused with a sentence) and the history lists the newest run first.</summary>
    [Fact]
    public async Task RunNowIsAdmittedOnceAtATimeAndTheHistoryIsNewestFirst()
    {
        using var tree = new TempTree();
        var clock = new FakeTimeProvider(Start);
        var gate = new TaskCompletionSource();
        var sessions = new ScriptedAutomationSessions { TurnGate = gate };
        var host = Host(tree, clock);
        await using var hostScope = host.ConfigureAwait(true);
        host.Attach(sessions);
        var saved = await host.Manager.SaveAsync(null, Draft(Cron("0 9 * * *")), Token).ConfigureAwait(true);

        var run = await host.Manager.RunNowAsync(saved.Entry.Id, Token).ConfigureAwait(true);
        var second = await Should.ThrowAsync<AutomationsSourceException>(() => host.Manager.RunNowAsync(saved.Entry.Id, Token)).ConfigureAwait(true);
        _ = gate.TrySetResult();
        await AutomationFixtures.EventuallyAsync(
            async () => (await host.Manager.HistoryAsync(saved.Entry.Id, Token).ConfigureAwait(true)).Any(static entry => entry.Status == HostedRunStatus.Succeeded),
            "the first run ended",
            Token).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMinutes(1));
        _ = await host.Manager.RunNowAsync(saved.Entry.Id, Token).ConfigureAwait(true);
        var history = await host.Manager.HistoryAsync(saved.Entry.Id, Token).ConfigureAwait(true);

        run.Status.ShouldBe(HostedRunStatus.Running);
        run.Trigger.ShouldBe("manual");
        second.Message.ShouldContain("already running", Case.Sensitive);
        history.Count(static entry => entry.Status != HostedRunStatus.Skipped).ShouldBeGreaterThanOrEqualTo(2);
        history.Select(static entry => entry.StartedAt).ShouldBe(history.Select(static entry => entry.StartedAt).OrderDescending());
    }
}
