using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>The editor on a head that receives no webhooks (the desktop): it says so, never offers the trigger, and asks for a schedule instead of a trigger.</summary>
public sealed class AutomationEditorScheduleOnlyTests
{
    /// <summary>The form reports that webhooks are not offered and carries the sentence that says why.</summary>
    [Fact]
    public void TheFormSaysThatOnlyTheScheduleIsOffered()
    {
        var model = new AutomationEditorModel(new FakeAutomationsManager { SupportsWebhooks = false }, null);

        model.SupportsWebhooks.ShouldBeFalse();
        model.ScheduleOnlyNote.ShouldBe(AutomationsCopy.ScheduleOnlyNote);
        model.WebhookEnabled.ShouldBeFalse();
    }

    /// <summary>A head with webhooks has no such note.</summary>
    [Fact]
    public void AHeadWithWebhooksHasNoNote()
    {
        var model = new AutomationEditorModel(new FakeAutomationsManager { SupportsWebhooks = true }, null);

        model.SupportsWebhooks.ShouldBeTrue();
        model.ScheduleOnlyNote.ShouldBeEmpty();
    }

    /// <summary>Without a schedule the message asks for a schedule, not for a webhook the head cannot receive; with the webhook head the message keeps naming both.</summary>
    [Fact]
    public async Task TheMissingTriggerMessageNamesOnlyWhatTheHeadOffers()
    {
        var desktop = new AutomationEditorModel(new FakeAutomationsManager { SupportsWebhooks = false }, null);
        var web = new AutomationEditorModel(new FakeAutomationsManager { SupportsWebhooks = true }, null);
        desktop.Name = "Name";
        desktop.Prompt = "Prompt";
        web.Name = "Name";
        web.Prompt = "Prompt";

        await desktop.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await web.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        desktop.Errors[AutomationsCopy.FieldTriggers].ShouldBe(AutomationsCopy.ScheduleRequired);
        web.Errors[AutomationsCopy.FieldTriggers].ShouldBe(AutomationsCopy.TriggersRequired);
    }
}
