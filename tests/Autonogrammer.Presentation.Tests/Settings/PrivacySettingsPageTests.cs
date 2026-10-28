using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Privacy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Privacy page: ready at once, read-only, and the five statements it makes.</summary>
public sealed class PrivacySettingsPageTests
{
    /// <summary>The page is ready before any load, belongs to the Privacy section and has nothing to edit.</summary>
    [Fact]
    public void IsReadyAtOnceAndNeverDirty()
    {
        var page = new PrivacySettingsPage();

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Section.ShouldBe(SettingsSection.Privacy);
        page.Title.ShouldBe(PrivacyCopy.Title);
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
    }

    /// <summary>Loading succeeds, keeps the page ready and changes nothing; saving is a no-op that succeeds.</summary>
    [Fact]
    public async Task LoadingAndSavingChangeNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var page = new PrivacySettingsPage();
        var rowsBefore = page.Rows;

        await page.LoadAsync(token).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Rows.ShouldBe(rowsBefore);
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>The page makes five statements, each with its own heading, in a fixed order, and each row's summary joins the heading and the sentence.</summary>
    [Fact]
    public void MakesFiveStatementsInOrder()
    {
        var rows = new PrivacySettingsPage().Rows;

        rows.Select(static row => row.Heading).ShouldBe(
            [
                PrivacyCopy.LeavesHeading,
                PrivacyCopy.IgnoreHeading,
                PrivacyCopy.TelemetryHeading,
                PrivacyCopy.RemoteHeading,
                PrivacyCopy.PrivacyModeHeading,
            ],
            ignoreOrder: false);
        rows.ShouldAllBe(static row => row.Text.Length > 0);
        rows[0].Summary.ShouldBe(PrivacyCopy.LeavesHeading + ". " + PrivacyCopy.LeavesText);
    }

    /// <summary>The statements about third parties are worded as what Autonogrammer does, not as a guarantee: the provider's own handling is said to be the provider's.</summary>
    [Fact]
    public void DoesNotPromiseWhatAProviderDoes()
    {
        PrivacyCopy.LeavesText.ShouldContain("set by that provider", Case.Sensitive);
        PrivacyCopy.PrivacyModeText.ShouldContain("Autonogrammer has no accounts", Case.Sensitive);
        PrivacyCopy.IgnoreText.ShouldContain("engine's own file tools follow the engine's rules", Case.Sensitive);
    }
}
