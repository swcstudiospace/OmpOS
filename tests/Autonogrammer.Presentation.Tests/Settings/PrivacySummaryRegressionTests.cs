using System.Reflection;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Privacy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>
/// The General page sentence and the Privacy page statements come from one copy class, and they agree with what the desktop app ships.
/// </summary>
public sealed class PrivacySummaryRegressionTests
{
    /// <summary>The General page sentence does not claim that Autonogrammer sends nothing, and it says what the page states about model requests.</summary>
    [Fact]
    public void SummaryDoesNotClaimThatNothingIsSent()
    {
        PrivacyCopy.Summary.ShouldNotContain("sends nothing", Case.Insensitive);
        PrivacyCopy.Summary.ShouldContain("no telemetry", Case.Sensitive);
        PrivacyCopy.Summary.ShouldContain("made by omp", Case.Sensitive);
    }

    /// <summary>The telemetry statement lists the requests the desktop app makes itself: the Network check, sign-in, cloud agents and the launch-time omp check.</summary>
    [Fact]
    public void TelemetryStatementListsTheRequestsAutonogrammerMakesItself()
    {
        PrivacyCopy.TelemetryText.ShouldContain("Network check", Case.Sensitive);
        PrivacyCopy.TelemetryText.ShouldContain("sign-in", Case.Sensitive);
        PrivacyCopy.TelemetryText.ShouldContain("cloud agents", Case.Sensitive);
        PrivacyCopy.TelemetryText.ShouldContain("newer omp", Case.Sensitive);
    }

    /// <summary>The model list request is attributed to omp, which makes it, on both the General page and the Privacy page.</summary>
    [Fact]
    public void ModelRequestsAreAttributedToOmp()
    {
        PrivacyCopy.TelemetryText.ShouldContain("model list, are made by omp", Case.Sensitive);
        PrivacyCopy.Summary.ShouldContain("Requests to model providers are made by omp", Case.Sensitive);
    }

    /// <summary>The privacy text lives only in PrivacyCopy: the settings copy class keeps no privacy constant.</summary>
    [Fact]
    public void SettingsCopyHoldsNoPrivacyText()
    {
        typeof(SettingsCopy).GetField("PrivacyStatement", BindingFlags.Public | BindingFlags.Static).ShouldBeNull();
        typeof(SettingsCopy).GetField("PrivacyHeading", BindingFlags.Public | BindingFlags.Static).ShouldBeNull();
    }
}
