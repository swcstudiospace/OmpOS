using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>The switch is two named toggles, and the hidden-surface chip is a polite live region.</summary>
public sealed class SurfaceSwitchViewTests
{
    /// <summary>The names, the toggle state and the chip match the host state.</summary>
    [Fact]
    public async Task TheSwitchAnnouncesSelectionAndTheHiddenChip()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var catalog = new MemoryCoworkCatalog();
            var surface = new SurfaceSwitchModel();
            surface.SetCoworkAvailable(true);
            var activity = new SurfaceActivityModel(surface, static () => [], static _ => { }, static _ => { }, catalog);
            var view = new SurfaceSwitchView();
            var window = new Window { Content = view, Width = 640, Height = 200 };
            try
            {
                window.Show();
                view.Attach(surface, activity);
                ShellHarness.Settle(window);
                AutomationProperties.GetName(view.CodeButton).ShouldBe("Code, workspace");
                AutomationProperties.GetName(view.CoworkButton).ShouldBe("Cowork, workspace");
                view.CodeButton.IsChecked.ShouldBe(true);
                view.CoworkButton.IsChecked.ShouldBe(false);
                view.CodeButton.Classes.Contains("segment").ShouldBeTrue();
                view.FindControl<Border>("segment-track").ShouldBeNull();

                catalog.Add(new CoworkSessionSummary
                {
                    Id = "task-1",
                    Title = "One",
                    Status = CoworkSessionStatus.Running,
                    CreatedAt = DateTimeOffset.UnixEpoch,
                    UpdatedAt = DateTimeOffset.UnixEpoch,
                });
                catalog.Add(new CoworkSessionSummary
                {
                    Id = "task-2",
                    Title = "Two",
                    Status = CoworkSessionStatus.Running,
                    CreatedAt = DateTimeOffset.UnixEpoch,
                    UpdatedAt = DateTimeOffset.UnixEpoch,
                });
                ShellHarness.Settle(window);
                var expected = CoworkCopy.RunningChip("Cowork", 2);
                view.Chip.IsVisible.ShouldBeTrue();
                view.Chip.Text.ShouldBe(expected);
                AutomationProperties.GetName(view.Chip).ShouldBe(expected);
                AutomationProperties.GetLiveSetting(view.Chip).ShouldBe(AutomationLiveSetting.Polite);

                view.CoworkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                surface.Selected.ShouldBe(SessionSurface.Cowork);
                view.CoworkButton.IsChecked.ShouldBe(true);
                view.CodeButton.IsChecked.ShouldBe(false);
            }
            finally
            {
                window.Close();
                activity.Dispose();
            }

            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }
}
