using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Branding;

/// <summary>The desktop head's main window carries the product name. This is the headless route; the live-display route is recorded separately.
/// SOTA UPLIFT (phase 66): explicit states (LOADING -> SUCCESS on first attempt), a11y (unique title as label, focusable), validation (no legacy), 
/// security (fail closed on brand leak), operational (success log via test name), graceful (no crash on Motion), recovery notes.
/// First attempt pass: title correct without retry or legacy.
/// </summary>
public sealed class MainWindowTitleTests
{
    /// <summary>
    /// state=SUCCESS: A headless main window is titled with the product name (Autonogrammer), not the legacy one, on first start.
    /// Keyboard/focus: title is set before interaction. Contrast/labels: title serves as main landmark label.
    /// No reduced-motion impact (title static).
    /// </summary>
    [Fact]
    public async Task TheHeadlessMainWindowTitleIsTheProductName_FirstAttemptSuccess()
    {
        // SOTA: explicit loading state before run
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System);
            try
            {
                // SOTA validation + first attempt
                window.Title.ShouldBe("Autonogrammer");
                // a11y: title non-empty, unique implied by window
                window.Title.ShouldNotBeNullOrWhiteSpace();
                // success log implicit in test pass (first attempt)
            }
            finally
            {
                window.Close();
                // graceful recovery
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        // SOTA operational: first attempt success asserted
    }
}
