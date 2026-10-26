using Avalonia.Animation;
using OmpOS.Native.Engine;
using OmpOS.Presentation;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>Reduced motion on the shared controls (XP-01, XP-08, D-20-08; criterion 5, motion half).</summary>
public sealed class ReducedMotionTests
{
    /// <summary>The Reduced setting puts the reduced-motion class on the window.</summary>
    [Fact]
    public async Task ReducedSetsTheClassOnTheWindow()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show(MotionPreference.Reduced);

            window.Classes.Contains("reduced-motion").ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>System and Full leave the window without the class (System resolves to Full, D-20-08).</summary>
    [Fact]
    public async Task SystemAndFullDoNotSetTheClass()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            ShellHarness.Show(MotionPreference.System).Classes.Contains("reduced-motion").ShouldBeFalse();
            ShellHarness.Show(MotionPreference.Full).Classes.Contains("reduced-motion").ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With reduced motion on, no shared control keeps a transition.</summary>
    [Fact]
    public async Task ReducedMotionRemovesTransitionsFromEverySharedControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show(MotionPreference.Reduced);

            foreach (var control in SharedControls(window))
            {
                (control.Transitions?.Count ?? 0).ShouldBe(0, $"{control.GetType().Name} must have no transitions under reduced motion");
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With full motion, the Button background transition is kept at 120 ms.</summary>
    [Fact]
    public async Task FullMotionKeepsTheButtonBackgroundTransition()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show(MotionPreference.Full);

            var transition = ShellHarness.Send(window).Transitions?.OfType<BrushTransition>().SingleOrDefault();

            transition.ShouldNotBeNull("the send control keeps its background transition under full motion");
            transition.Duration.ShouldBe(TimeSpan.FromMilliseconds(120));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The main view carries the class itself under Reduced, so a head with no window (Android) can style on it (M-22).</summary>
    [Fact]
    public async Task ReducedSetsTheClassOnTheMainViewItself()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var main = new MainView(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), Environment.CurrentDirectory, prompts: null, MotionPreference.Reduced);

            main.Classes.Contains(MainView.ReducedMotionClass).ShouldBeTrue();
            main.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>System and Full leave the main view without the class, as they leave the window (System resolves to Full, D-20-08).</summary>
    [Fact]
    public async Task SystemAndFullLeaveTheMainViewWithoutTheClass()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            foreach (var motion in new[] { MotionPreference.System, MotionPreference.Full })
            {
                var main = new MainView(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), Environment.CurrentDirectory, prompts: null, motion);

                main.Classes.Contains(MainView.ReducedMotionClass).ShouldBeFalse();
                main.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static IEnumerable<Animatable> SharedControls(MainWindow window)
    {
        yield return ShellHarness.SkipLink(window);
        yield return ShellHarness.Rows(window);
        yield return ShellHarness.Scroll(window);
        yield return ShellHarness.Prompt(window);
        yield return ShellHarness.Send(window);
        yield return ShellHarness.Stop(window);
    }
}
