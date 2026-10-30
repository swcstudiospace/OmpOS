using System.Xml.Linq;
using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The native head binds the same <c>Autonogrammer.Presentation</c> view models the web head binds, and holds no copy of them
/// (XP-08, ADR-0004 drift guard; criterion 3, native half).
/// </summary>
public sealed class PresentationBindingTests
{
    /// <summary>The transcript's data context is the one presentation session view model the shell owns.</summary>
    [Fact]
    public async Task TranscriptIsBoundToThePresentationSessionViewModel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            var runtime = ShellHarness.RuntimeOf(window);
            ShellHarness.Transcript(window).DataContext.ShouldBeSameAs(runtime.Session);
            runtime.Session.ShouldBeOfType<SessionViewModel>();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The composer's data context is the one presentation composer view model the shell owns.</summary>
    [Fact]
    public async Task ComposerIsBoundToThePresentationComposerViewModel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();

            var runtime = ShellHarness.RuntimeOf(window);
            ShellHarness.Composer(window).DataContext.ShouldBeSameAs(runtime.Composer);
            runtime.Composer.ShouldBeOfType<ComposerViewModel>();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The explicit mapping returns the native view for each mapped view model type.</summary>
    [Fact]
    public async Task ViewMappingReturnsTheNativeViewForEachMappedType()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = new SessionViewModel();
            var composer = new ComposerViewModel(static _ => Task.FromResult(false), static () => Task.CompletedTask);

            var sessionView = ViewMapping.Create(session);
            var composerView = ViewMapping.Create(composer);

            sessionView.ShouldBeOfType<SessionView>().DataContext.ShouldBeSameAs(session);
            composerView.ShouldBeOfType<ComposerView>().DataContext.ShouldBeSameAs(composer);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each Agent Panel view model maps to the view that draws it, with the model as its data context.</summary>
    [Fact]
    public async Task ViewMappingReturnsTheAgentPanelViewForEachPanelViewModel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var agent = fixture.Panel.Selected!;

            ViewMapping.Create(fixture.Catalog).ShouldBeOfType<AgentRailView>().DataContext.ShouldBeSameAs(fixture.Catalog);
            ViewMapping.Create(fixture.Panel).ShouldBeOfType<PanelStateView>().DataContext.ShouldBeSameAs(fixture.Panel);
            ViewMapping.Create(agent).ShouldBeOfType<ThreadHeaderView>().DataContext.ShouldBeSameAs(agent);
            ViewMapping.Create(agent.Timeline).ShouldBeOfType<PipelineStripView>().DataContext.ShouldBeSameAs(agent.Timeline);
            Should.Throw<ArgumentOutOfRangeException>(() => ViewMapping.Create(agent.Context), "the task context has no view of its own in the panel");
            Should.Throw<ArgumentOutOfRangeException>(() => ViewMapping.Create(agent.Steering), "the steering history has no view of its own in the panel");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A type with no explicit mapping is rejected, so no view is guessed.</summary>
    [Fact]
    public async Task ViewMappingRejectsAViewModelWithNoMapping()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Should.Throw<ArgumentOutOfRangeException>(() => ViewMapping.Create(new object()));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The native assembly references Autonogrammer.Presentation and declares no type in the presentation namespaces.</summary>
    [Fact]
    public void NativeAssemblyReferencesPresentationAndDeclaresNoCopy()
    {
        var native = typeof(App).Assembly;

        native.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty).ShouldContain("Autonogrammer.Presentation", StringComparer.Ordinal);
        typeof(SessionViewModel).Assembly.GetName().Name.ShouldBe("Autonogrammer.Presentation");
        native.GetTypes()
            .Select(static type => type.FullName)
            .Where(static name => name is not null && name.StartsWith("Autonogrammer.Presentation", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    /// <summary>The native project and the web head's Razor library both reference the one presentation project.</summary>
    [Theory]
    [InlineData("apps/Autonogrammer.Native/Autonogrammer.Native.csproj")]
    [InlineData("src/Autonogrammer.UI/Autonogrammer.UI.csproj")]
    public void HeadProjectReferencesThePresentationProject(string relativeProject)
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), relativeProject.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).ShouldBeTrue($"{relativeProject} must exist");

        XDocument.Load(path)
            .Descendants("ProjectReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Any(static include => include is not null && include.EndsWith("Autonogrammer.Presentation.csproj", StringComparison.Ordinal))
            .ShouldBeTrue($"{relativeProject} must reference Autonogrammer.Presentation");
    }
}
