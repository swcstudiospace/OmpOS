using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>Chromium state sentences, the acquisition dialog and option refusals, over a scripted source.</summary>
public sealed class BrowserUseSettingsPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Each Chromium state renders its sentence and its one action.</summary>
    [Theory]
    [InlineData(BrowserUseChromiumState.BrowserNotInstalled, null, BrowserUseCopy.BrowserNotInstalledSentence, BrowserUseCopy.BrowserNotInstalledAction)]
    [InlineData(BrowserUseChromiumState.ManagedBrowserNotPinned, null, BrowserUseCopy.ManagedBrowserNotPinnedSentence, BrowserUseCopy.ManagedBrowserNotPinnedAction)]
    [InlineData(BrowserUseChromiumState.BrowserTooOld, null, BrowserUseCopy.BrowserTooOldSentence, BrowserUseCopy.BrowserTooOldAction)]
    [InlineData(BrowserUseChromiumState.Ready, "149", "Chromium 149 is ready.", BrowserUseCopy.ReadyAction)]
    public async Task EachStateRendersItsSentenceAndFixAction(
        BrowserUseChromiumState state,
        string? version,
        string sentence,
        string action)
    {
        var source = new ScriptedBrowserUseSource(Snapshot(state, version, offer: null));
        var page = new BrowserUseSettingsPage(source);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.ChromiumSentence.ShouldBe(sentence);
        page.FixAction.ShouldBe(action);
        page.IsAcquisitionVisible.ShouldBeFalse();
        page.OpenAcquisition().ShouldBeNull();
        source.AcquireCalls.ShouldBe(0);
    }

    /// <summary>The install action is hidden unless a manifest row exists, and the dialog names version, host, size and licence.</summary>
    [Fact]
    public async Task AcquisitionIsHiddenWithoutARowAndTheDialogNamesTheRow()
    {
        var hidden = new BrowserUseSettingsPage(new ScriptedBrowserUseSource(Snapshot(BrowserUseChromiumState.BrowserNotInstalled, null, offer: null)));
        await hidden.LoadAsync(Token).ConfigureAwait(true);
        hidden.IsAcquisitionVisible.ShouldBeFalse();

        var source = new ScriptedBrowserUseSource(Snapshot(
            BrowserUseChromiumState.BrowserNotInstalled,
            null,
            new BrowserUseManifestOffer("131.0.6778.0", "storage.googleapis.com", 87_654_321, BrowserUseCopy.LicenceLine)));
        var page = new BrowserUseSettingsPage(source);
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.IsAcquisitionVisible.ShouldBeTrue();
        var dialog = page.OpenAcquisition().ShouldNotBeNull();
        dialog.Text.ShouldContain("131.0.6778.0");
        dialog.Text.ShouldContain("storage.googleapis.com");
        dialog.Text.ShouldContain("87654321");
        dialog.Text.ShouldContain(BrowserUseCopy.LicenceLine);
        source.AcquireCalls.ShouldBe(0);

        dialog.Dismiss();
        source.AcquireCalls.ShouldBe(0);

        await dialog.ConfirmAsync(Token).ConfigureAwait(true);
        source.AcquireCalls.ShouldBe(1);
        dialog.IsConfirmed.ShouldBeTrue();
    }

    /// <summary>Open and trusted local origins each show a one-line risk text. The other modes do not.</summary>
    [Fact]
    public async Task OpenAndLocalOriginsShowARiskLine()
    {
        var page = new BrowserUseSettingsPage(new ScriptedBrowserUseSource(Snapshot(BrowserUseChromiumState.Ready, "149", offer: null)));
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.PolicyRiskText.ShouldBeEmpty();
        page.LocalOriginsRiskText.ShouldBeEmpty();

        page.PolicyMode = BrowserUsePolicyMode.Open;
        page.PolicyRiskText.ShouldBe(BrowserUseCopy.OpenRisk);

        page.PolicyMode = BrowserUsePolicyMode.Strict;
        page.PolicyRiskText.ShouldBeEmpty();

        page.LocalOriginsTrusted = true;
        page.LocalOriginsRiskText.ShouldBe(BrowserUseCopy.LocalOriginsRisk);
    }

    /// <summary>A source refusal is shown and names the key, never a silent save.</summary>
    [Fact]
    public async Task InvalidOptionValuesAreRefusedWithTheKeyName()
    {
        var source = new ScriptedBrowserUseSource(Snapshot(BrowserUseChromiumState.Ready, "149", offer: null))
        {
            Save = (_, _) => throw new SettingsSourceException("Cowork:BrowserUse:PolicyMode is not a defined value."),
        };
        var page = new BrowserUseSettingsPage(source);
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.PolicyMode = BrowserUsePolicyMode.Open;
        var saved = await page.SaveAsync(Token).ConfigureAwait(true);

        saved.ShouldBeFalse();
        page.SaveError.ShouldContain("Cowork:BrowserUse:PolicyMode");
        page.SaveError.ShouldNotContain("Open");
        page.IsDirty.ShouldBeTrue();
    }

    private static BrowserUseSnapshot Snapshot(BrowserUseChromiumState state, string? version, BrowserUseManifestOffer? offer) =>
        new(state, version, BrowserUseSettingsValues.Defaults, offer);

    private sealed class ScriptedBrowserUseSource : IBrowserUseSettingsSource
    {
        public ScriptedBrowserUseSource(BrowserUseSnapshot snapshot) => Snapshot = snapshot;

        public BrowserUseSnapshot Snapshot { get; }

        public int AcquireCalls { get; private set; }

        public Func<BrowserUseSettingsValues, CancellationToken, Task>? Save { get; init; }

        public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);

        public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) =>
            Save?.Invoke(values, cancellationToken) ?? Task.CompletedTask;

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            AcquireCalls++;
            return Task.CompletedTask;
        }
    }
}
