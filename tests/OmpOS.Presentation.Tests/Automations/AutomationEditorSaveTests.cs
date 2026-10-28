using System.Globalization;
using OmpOS.Presentation.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Automations;

/// <summary>
/// Saving from the editor: what the manager receives, the webhook secret that is shown once and then gone, the failure text, cancelling, and
/// that the secret appears in no property, message or text output except the one that shows it.
/// </summary>
public sealed class AutomationEditorSaveTests
{
    private const string Canary = "whsec-canary-7f3a91";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A valid form is saved as a new automation: the manager gets a create with the typed draft, and the form becomes an edit of the saved entry.</summary>
    [Fact]
    public async Task SavingANewAutomationSendsTheDraftAndSwitchesToEditing()
    {
        var manager = new FakeAutomationsManager();
        var model = ValidLocal(manager);
        var raised = 0;
        model.Saved += (_, _) => raised++;

        await model.SaveAsync(Token).ConfigureAwait(true);

        var save = manager.Saves.Single();
        save.Id.ShouldBeNull();
        save.Draft.Name.ShouldBe("Nightly triage");
        save.Draft.TargetKind.ShouldBe("local");
        model.SavedEntry.ShouldNotBeNull().Id.ShouldBe("auto-1");
        model.IsEdit.ShouldBeTrue();
        model.IsSaving.ShouldBeFalse();
        model.SaveFailure.ShouldBeEmpty();
        raised.ShouldBe(1);

        model.Name = "Nightly triage v2";
        await model.SaveAsync(Token).ConfigureAwait(true);

        manager.Saves.Count.ShouldBe(2);
        manager.Saves[1].Id.ShouldBe("auto-1");
        manager.Snapshot().Count.ShouldBe(1);
    }

    /// <summary>
    /// A save that generated a webhook secret shows it exactly once: it is in RevealedSecret after the save, a second read after the user
    /// confirmed is empty, and saving again does not bring it back.
    /// </summary>
    [Fact]
    public async Task GeneratedSecretIsShownOnceAndClearedOnAcknowledge()
    {
        var manager = new FakeAutomationsManager { SecretOnSave = Canary };
        var model = ValidLocal(manager);
        model.WebhookEnabled = true;

        await model.SaveAsync(Token).ConfigureAwait(true);

        model.RevealedSecret.ShouldBe(Canary);
        model.WebhookPath.ShouldBe("/webhooks/auto-1");
        model.HasWebhookSecret.ShouldBeTrue();

        model.AcknowledgeSecret();

        model.RevealedSecret.ShouldBeEmpty();

        model.Name = "Renamed";
        await model.SaveAsync(Token).ConfigureAwait(true);

        model.RevealedSecret.ShouldBeEmpty();
        manager.Saves.Count.ShouldBe(2);
    }

    /// <summary>A save without a webhook shows no secret panel, and one with a webhook whose secret the service did not generate shows none either.</summary>
    [Fact]
    public async Task NoSecretIsShownWhenNoneWasGenerated()
    {
        var withoutHook = ValidLocal(new FakeAutomationsManager { SecretOnSave = Canary });
        await withoutHook.SaveAsync(Token).ConfigureAwait(true);
        withoutHook.RevealedSecret.ShouldBeEmpty();

        var quiet = ValidLocal(new FakeAutomationsManager());
        quiet.WebhookEnabled = true;
        await quiet.SaveAsync(Token).ConfigureAwait(true);
        quiet.RevealedSecret.ShouldBeEmpty();
    }

    /// <summary>An expected failure becomes the failure text, keeps the form as it was and lets the user save again; nothing is saved.</summary>
    [Fact]
    public async Task RefusedSaveShowsTheReasonAndKeepsTheForm()
    {
        var manager = new FakeAutomationsManager();
        manager.Fail("Save", new AutomationsSourceException("An automation with this name already exists."));
        var model = ValidLocal(manager);
        var raised = 0;
        model.Saved += (_, _) => raised++;

        await model.SaveAsync(Token).ConfigureAwait(true);

        model.SaveFailure.ShouldBe("An automation with this name already exists.");
        model.SavedEntry.ShouldBeNull();
        model.IsEdit.ShouldBeFalse();
        model.IsSaving.ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
        model.Name.ShouldBe("Nightly triage");
        model.RevealedSecret.ShouldBeEmpty();
        raised.ShouldBe(0);

        manager.Fail("Save", null);
        await model.SaveAsync(Token).ConfigureAwait(true);

        model.SaveFailure.ShouldBeEmpty();
        model.SavedEntry.ShouldNotBeNull();
    }

    /// <summary>A manager exception that is not an expected failure is a defect: it escapes and the form is not left saving.</summary>
    [Fact]
    public async Task UnexpectedExceptionEscapesAndTheFormIsNotLeftSaving()
    {
        var manager = new FakeAutomationsManager();
        manager.Fail("Save", new InvalidOperationException("defect"));
        var model = ValidLocal(manager);

        await Should.ThrowAsync<InvalidOperationException>(() => model.SaveAsync(Token)).ConfigureAwait(true);

        model.IsSaving.ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
        model.SavedEntry.ShouldBeNull();
    }

    /// <summary>Cancelling a save throws and leaves the form as it was: not saving, no failure text, nothing saved.</summary>
    [Fact]
    public async Task CancellingASaveLeavesTheFormAsItWas()
    {
        var manager = new FakeAutomationsManager();
        manager.Hold("Save");
        var model = ValidLocal(manager);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var pending = model.SaveAsync(source.Token);
        model.IsSaving.ShouldBeTrue();
        model.CanSave.ShouldBeFalse();
        await source.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(Token)).ConfigureAwait(true);

        model.IsSaving.ShouldBeFalse();
        model.SaveFailure.ShouldBeEmpty();
        model.SavedEntry.ShouldBeNull();
        model.CanSave.ShouldBeTrue();
    }

    /// <summary>While a save runs, a second save does not reach the manager.</summary>
    [Fact]
    public async Task SecondSaveWhileSavingMakesNoSecondCall()
    {
        var manager = new FakeAutomationsManager();
        manager.Hold("Save");
        var model = ValidLocal(manager);

        var first = model.SaveAsync(Token);
        await model.SaveAsync(Token).ConfigureAwait(true);
        manager.Release("Save");
        await first.ConfigureAwait(true);

        manager.CountOf("Save").ShouldBe(1);
    }

    /// <summary>The webhook secret is in RevealedSecret and nowhere else: not in any text output, message, row, path or failure.</summary>
    [Fact]
    public async Task SecretAppearsOnlyInRevealedSecret()
    {
        var manager = new FakeAutomationsManager { SecretOnSave = Canary };
        var model = ValidLocal(manager);
        model.WebhookEnabled = true;

        await model.SaveAsync(Token).ConfigureAwait(true);

        model.RevealedSecret.ShouldBe(Canary);
        var saved = model.SavedEntry.ShouldNotBeNull();
        var row = new HostedAutomationRowModel(saved);
        var result = new AutomationSaveResult(saved, Canary);
        var visible = new List<string>
        {
            Convert.ToString(model, CultureInfo.InvariantCulture)!,
            saved.ToString(),
            saved.Draft.ToString(),
            saved.WebhookPath,
            model.WebhookPath,
            model.SaveFailure,
            model.WhyDisabled,
            model.CronPreviewError,
            Convert.ToString(row, CultureInfo.InvariantCulture)!,
            row.TriggerText,
            row.TargetText,
            row.LastResultText,
            row.WebhookText,
            row.AccessibleName,
            Convert.ToString(result, CultureInfo.InvariantCulture)!,
            result.Entry.ToString(),
        };
        visible.AddRange(model.Errors.Values);
        visible.AddRange(model.CronPreviewLines);

        foreach (var text in visible)
        {
            text.ShouldNotContain(Canary, Case.Sensitive);
        }

        model.AcknowledgeSecret();
        model.RevealedSecret.ShouldBeEmpty();
        saved.ToString().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A failed save never repeats the secret either, even when the failure comes after a secret was shown.</summary>
    [Fact]
    public async Task FailureTextNeverCarriesTheSecret()
    {
        var manager = new FakeAutomationsManager { SecretOnSave = Canary };
        var model = ValidLocal(manager);
        model.WebhookEnabled = true;
        await model.SaveAsync(Token).ConfigureAwait(true);
        model.AcknowledgeSecret();
        manager.Fail("Save", new AutomationsSourceException("The service is busy."));

        model.Name = "Again";
        await model.SaveAsync(Token).ConfigureAwait(true);

        model.SaveFailure.ShouldBe("The service is busy.");
        model.SaveFailure.ShouldNotContain(Canary, Case.Sensitive);
        model.RevealedSecret.ShouldBeEmpty();
    }

    /// <summary>Rotating replaces the secret: the new one is shown once, the manager is asked for the saved automation, and acknowledging clears it.</summary>
    [Fact]
    public async Task RotatingShowsTheNewSecretOnce()
    {
        var entry = AutomationFixtures.Entry(
            draft: AutomationFixtures.Draft() with { Triggers = [AutomationFixtures.Webhook()] },
            webhookPath: "/webhooks/auto-1",
            hasSecret: true);
        var manager = new FakeAutomationsManager(entry) { RotatedSecret = Canary };
        var model = new AutomationEditorModel(manager, entry);
        model.CanRotateSecret.ShouldBeTrue();

        await model.RotateSecretAsync(Token).ConfigureAwait(true);

        manager.Calls.ShouldContain("Rotate:auto-1", StringComparer.Ordinal);
        model.RevealedSecret.ShouldBe(Canary);
        model.SaveFailure.ShouldBeEmpty();
        Convert.ToString(model, CultureInfo.InvariantCulture)!.ShouldNotContain(Canary, Case.Sensitive);

        model.AcknowledgeSecret();
        model.RevealedSecret.ShouldBeEmpty();
    }

    /// <summary>A failed rotation shows the reason and no secret; a new automation cannot be rotated and makes no call.</summary>
    [Fact]
    public async Task FailedRotationShowsTheReasonAndNewAutomationsCannotRotate()
    {
        var entry = AutomationFixtures.Entry(
            draft: AutomationFixtures.Draft() with { Triggers = [AutomationFixtures.Webhook()] },
            webhookPath: "/webhooks/auto-1",
            hasSecret: true);
        var manager = new FakeAutomationsManager(entry);
        manager.Fail("Rotate", new AutomationsSourceException("The secret store is read-only."));
        var model = new AutomationEditorModel(manager, entry);

        await model.RotateSecretAsync(Token).ConfigureAwait(true);

        model.SaveFailure.ShouldBe("The secret store is read-only.");
        model.RevealedSecret.ShouldBeEmpty();

        var fresh = ValidLocal(manager);
        fresh.CanRotateSecret.ShouldBeFalse();
        await fresh.RotateSecretAsync(Token).ConfigureAwait(true);
        manager.CountOf("Rotate").ShouldBe(1);
    }

    /// <summary>Editing an existing automation sends an update for its identifier, not a create.</summary>
    [Fact]
    public async Task EditingSendsAnUpdateForTheSameIdentifier()
    {
        var entry = AutomationFixtures.Entry("nightly");
        var manager = new FakeAutomationsManager(entry);
        var model = new AutomationEditorModel(manager, entry);
        model.IsEdit.ShouldBeTrue();
        model.CanSave.ShouldBeTrue();

        model.Prompt = "A new prompt.";
        await model.SaveAsync(Token).ConfigureAwait(true);

        manager.Saves.Single().Id.ShouldBe("nightly");
        manager.Saves.Single().Draft.Prompt.ShouldBe("A new prompt.");
        manager.Snapshot().Single().Draft.Prompt.ShouldBe("A new prompt.");
    }

    private static AutomationEditorModel ValidLocal(FakeAutomationsManager manager) =>
        new(manager, null)
        {
            Name = "Nightly triage",
            Prompt = "Triage new issues.",
            CronText = "0 9 * * 1-5",
        };
}
