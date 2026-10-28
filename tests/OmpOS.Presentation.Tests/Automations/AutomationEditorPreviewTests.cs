using OmpOS.Presentation.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Automations;

/// <summary>The schedule preview: the next runs of a valid expression, the reason an invalid one gives, and what an edit or a cancel does to it.</summary>
public sealed class AutomationEditorPreviewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A valid expression shows its next runs as numbered UTC lines, and the manager is asked for five in the chosen zone.</summary>
    [Fact]
    public async Task ValidExpressionShowsNumberedUtcLines()
    {
        var manager = new FakeAutomationsManager();
        var model = Form(manager);
        model.TimeZone = "Europe/Berlin";

        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        model.CronPreviewLines.ShouldBe(["1. 2026-11-02 09:00 UTC", "2. 2026-11-03 09:00 UTC"]);
        model.CronPreviewError.ShouldBeEmpty();
        model.IsPreviewing.ShouldBeFalse();
        manager.Previews.Single().ShouldBe(("0 9 * * 1-5", "Europe/Berlin", 5));
        model.Errors.ContainsKey("cron").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
    }

    /// <summary>The expression is sent trimmed, and an empty time zone is sent as UTC.</summary>
    [Fact]
    public async Task ExpressionIsTrimmedAndEmptyZoneMeansUtc()
    {
        var manager = new FakeAutomationsManager();
        var model = Form(manager);
        model.CronText = "  */15 * * * *  ";
        model.TimeZone = string.Empty;

        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        manager.Previews.Single().ShouldBe(("*/15 * * * *", "UTC", 5));
    }

    /// <summary>An invalid expression shows the manager's reason, no lines, and keeps Save unavailable until the expression changes.</summary>
    [Fact]
    public async Task InvalidExpressionShowsTheReasonAndBlocksSaveUntilItChanges()
    {
        var manager = new FakeAutomationsManager
        {
            PreviewResult = static _ => new AutomationCronPreview([], "A cron expression has five fields."),
        };
        var model = Form(manager);
        model.CronText = "every day";

        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        model.CronPreviewError.ShouldBe("A cron expression has five fields.");
        model.CronPreviewLines.ShouldBeEmpty();
        model.Errors["cron"].ShouldBe("A cron expression has five fields.");
        model.CanSave.ShouldBeFalse();
        model.WhyDisabled.ShouldContain("Schedule", Case.Sensitive);

        model.CronText = "0 9 * * *";

        model.CronPreviewError.ShouldBeEmpty();
        model.Errors.ContainsKey("cron").ShouldBeFalse();
        model.CanSave.ShouldBeTrue();
    }

    /// <summary>An expression that is valid but never fires says so instead of showing an empty list.</summary>
    [Fact]
    public async Task ExpressionThatNeverFiresSaysSo()
    {
        var manager = new FakeAutomationsManager
        {
            PreviewResult = static _ => new AutomationCronPreview([], string.Empty),
        };
        var model = Form(manager);
        model.CronText = "0 0 31 2 *";

        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        model.CronPreviewError.ShouldBe(AutomationsCopy.CronNeverRuns);
        model.CronPreviewLines.ShouldBeEmpty();
        model.CanSave.ShouldBeFalse();
    }

    /// <summary>An empty expression makes no call and clears an earlier preview.</summary>
    [Fact]
    public async Task EmptyExpressionMakesNoCallAndClearsThePreview()
    {
        var manager = new FakeAutomationsManager();
        var model = Form(manager);
        await model.PreviewCronAsync(Token).ConfigureAwait(true);
        model.CronPreviewLines.Count.ShouldBe(2);

        model.CronText = string.Empty;
        model.CronPreviewLines.ShouldBeEmpty();
        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        manager.CountOf("Preview").ShouldBe(1);
        model.CronPreviewLines.ShouldBeEmpty();
        model.CronPreviewError.ShouldBeEmpty();
    }

    /// <summary>A manager that cannot compute the preview gives the reason as the preview error and never throws it.</summary>
    [Fact]
    public async Task ManagerFailureBecomesThePreviewError()
    {
        var manager = new FakeAutomationsManager();
        manager.Fail("Preview", new AutomationsSourceException("The scheduler is unreachable."));
        var model = Form(manager);

        await model.PreviewCronAsync(Token).ConfigureAwait(true);

        model.CronPreviewError.ShouldBe("The scheduler is unreachable.");
        model.CronPreviewLines.ShouldBeEmpty();
        model.IsPreviewing.ShouldBeFalse();
    }

    /// <summary>Editing the expression while a preview is running drops that preview's result: the lines belong to a different expression.</summary>
    [Fact]
    public async Task StalePreviewResultIsDropped()
    {
        var manager = new FakeAutomationsManager();
        manager.Hold("Preview");
        var model = Form(manager);

        var pending = model.PreviewCronAsync(Token);
        model.IsPreviewing.ShouldBeTrue();
        model.CronText = "30 8 * * *";
        manager.Release("Preview");
        await pending.ConfigureAwait(true);

        model.CronPreviewLines.ShouldBeEmpty();
        model.CronPreviewError.ShouldBeEmpty();
        model.IsPreviewing.ShouldBeFalse();
    }

    /// <summary>Cancelling a preview throws and keeps the lines that were already shown.</summary>
    [Fact]
    public async Task CancellingAPreviewKeepsTheEarlierLines()
    {
        var manager = new FakeAutomationsManager();
        var model = Form(manager);
        await model.PreviewCronAsync(Token).ConfigureAwait(true);
        manager.Hold("Preview");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var pending = model.PreviewCronAsync(source.Token);
        await source.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(Token)).ConfigureAwait(true);

        model.CronPreviewLines.Count.ShouldBe(2);
        model.IsPreviewing.ShouldBeFalse();
        model.CronPreviewError.ShouldBeEmpty();
    }

    private static AutomationEditorModel Form(FakeAutomationsManager manager) =>
        new(manager, null)
        {
            Name = "Nightly triage",
            Prompt = "Triage new issues.",
            CronText = "0 9 * * 1-5",
        };
}
