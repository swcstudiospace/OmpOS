using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The acquisition dialog runs one acquisition at a time, shows a refusal, and stops a running acquisition when dismissed.</summary>
public sealed class BrowserUseAcquisitionDialogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static BrowserUseManifestOffer Offer() => new("131.0.6778.0", "storage.googleapis.com", 87_654_321, BrowserUseCopy.LicenceLine);

    /// <summary>A second confirm while an acquisition runs starts nothing; the first one finishes without an error.</summary>
    [Fact]
    public async Task ASecondConfirmWhileOneRunsStartsNothing()
    {
        var release = new TaskCompletionSource();
        var calls = 0;
        var dialog = new BrowserUseAcquisitionDialog(Offer(), async token =>
        {
            calls++;
            await release.Task.WaitAsync(token).ConfigureAwait(true);
        });

        var first = dialog.ConfirmAsync(Token);
        await dialog.ConfirmAsync(Token).ConfigureAwait(true);
        calls.ShouldBe(1);

        release.SetResult();
        await first.ConfigureAwait(true);
        dialog.ErrorText.ShouldBeNull();
    }

    /// <summary>A refused acquisition shows its reason and leaves the dialog open, so confirming again tries again.</summary>
    [Fact]
    public async Task ARefusedAcquisitionShowsItsReasonAndCanBeTriedAgain()
    {
        var calls = 0;
        var dialog = new BrowserUseAcquisitionDialog(Offer(), _ =>
        {
            calls++;
            return Task.FromException(new SettingsSourceException("The archive is unreachable."));
        });

        await dialog.ConfirmAsync(Token).ConfigureAwait(true);
        dialog.ErrorText.ShouldBe("The archive is unreachable.");

        await dialog.ConfirmAsync(Token).ConfigureAwait(true);
        calls.ShouldBe(2);
        dialog.ErrorText.ShouldBe("The archive is unreachable.");
    }

    /// <summary>Dismissing the dialog stops a running acquisition, and the stopped acquisition is not reported as an error.</summary>
    [Fact]
    public async Task DismissingStopsARunningAcquisitionWithoutAnError()
    {
        var started = new TaskCompletionSource();
        var dialog = new BrowserUseAcquisitionDialog(Offer(), async cancellationToken =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
        });

        var running = dialog.ConfirmAsync(Token);
        await started.Task.ConfigureAwait(true);
        dialog.Dismiss();
        await running.ConfigureAwait(true);

        dialog.ErrorText.ShouldBeNull();
    }
}
