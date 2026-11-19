using Autonogrammer.Presentation.Settings.Privacy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>
/// The Privacy statements must describe the shipped account layer: a signed-in account stores a user id, an email address and a refresh token on this
/// machine, and the statements must not deny that accounts exist.
/// </summary>
public sealed class PrivacyCopyAccountRegressionTests
{
    /// <summary>No statement on the page says that Autonogrammer has no accounts; the account layer ships with the desktop app.</summary>
    [Fact]
    public void NoStatementDeniesAccounts()
    {
        PrivacyCopy.Rows.ShouldAllBe(
            static row => !row.Text.Contains("no accounts", StringComparison.OrdinalIgnoreCase),
            "no statement denies that accounts exist");
    }

    /// <summary>The Privacy Mode statement names what a signed-in account keeps on this machine and that signing out clears it.</summary>
    [Fact]
    public void PrivacyModeStatementNamesWhatAnAccountStores()
    {
        PrivacyCopy.PrivacyModeText.ShouldContain("user id", Case.Sensitive);
        PrivacyCopy.PrivacyModeText.ShouldContain("email address", Case.Sensitive);
        PrivacyCopy.PrivacyModeText.ShouldContain("refresh token", Case.Sensitive);
        PrivacyCopy.PrivacyModeText.ShouldContain("Signing out clears them", Case.Sensitive);
    }

    /// <summary>The statement about where data goes does not deny a server of its own, because cloud agents send prompts to the Autonogrammer API server.</summary>
    [Fact]
    public void LeavesStatementNamesTheCloudApi()
    {
        PrivacyCopy.LeavesText.ShouldContain("AUTONOGRAMMER_API_BASE_URL", Case.Sensitive);
        PrivacyCopy.LeavesText.ShouldNotContain("no server of its own that receives your code or prompts", Case.Sensitive);
    }
}
