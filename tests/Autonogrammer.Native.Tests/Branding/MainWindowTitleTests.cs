using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Branding;

/// <summary>Verifies the desktop window title on the headless route.</summary>
public sealed class MainWindowTitleTests
{
    /// <summary>The main window is titled with the product name.</summary>
    [Fact]
    public async Task TheHeadlessMainWindowTitleIsTheProductName_FirstAttemptSuccess()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            using var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System);
            try
            {
                window.Title.ShouldBe("Autonogrammer");
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
