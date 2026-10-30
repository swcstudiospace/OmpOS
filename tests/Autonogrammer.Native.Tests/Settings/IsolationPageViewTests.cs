using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Isolation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The isolation page shows the level, the notices and the continue-without-isolation commands.</summary>
public sealed class IsolationPageViewTests
{
    /// <summary>The view renders the level, the network notice, the risk sentences and the named Continue and Stop commands.</summary>
    [Fact]
    public async Task RendersLevelNoticesAndConsentCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new IsolationChipModel("None", "bwrap_missing", needsConsent: true);
            model.Answer.ShouldBeNull();
            var view = new IsolationPageView { DataContext = model };
            var window = new Window { Width = 900, Height = 900, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                var texts = SettingsTree.Texts(view);
                texts.ShouldContain(model.LevelText);
                texts.ShouldContain(IsolationChipModel.NetworkUnrestricted);
                texts.ShouldContain(IsolationChipModel.NotConfinedLaunches);
                texts.ShouldContain(IsolationChipModel.OmpReadableRisk);
                texts.ShouldContain(IsolationChipModel.ContinueWithoutIsolation);
                texts.ShouldContain("IsolationUnavailable(bwrap_missing)");
                model.LevelText.ShouldContain(IsolationChipModel.NetworkUnrestricted);
                SettingsTree.Has<Button>(view, "Continue without isolation").ShouldBeTrue();
                SettingsTree.Has<Button>(view, "Stop").ShouldBeTrue();
                var continueButton = SettingsTree.Visible<Button>(view).Single(button => button.Content as string == "Continue without isolation");
                var stopButton = SettingsTree.Visible<Button>(view).Single(button => button.Content as string == "Stop");
                continueButton.Command.ShouldBeSameAs(model.ContinueCommand);
                stopButton.Command.ShouldBeSameAs(model.StopCommand);
                SettingsTree.Unnamed(view).ShouldBeEmpty();
                stopButton.Command!.Execute(null);
                model.Answer.ShouldBe(false);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }
}
