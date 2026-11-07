using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Branding;

/// <summary>The desktop head's main window carries the product name. This is the headless route; the live-display route is recorded separately.</summary>
public sealed class MainWindowTitleTests
{
    /// <summary>A headless main window is titled with the product name, not the legacy one.</summary>
    [Fact]
    public async Task TheHeadlessMainWindowTitleIsTheProductName()
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
