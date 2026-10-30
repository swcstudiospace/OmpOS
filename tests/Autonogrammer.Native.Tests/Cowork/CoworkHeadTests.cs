using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Memory;
using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>Accessible names, focus order, no colour literals and reduced motion for the Cowork views.</summary>
public sealed class CoworkHeadTests
{
    /// <summary>Every control on the three views has an accessible name, in keyboard order, with no colour literal.</summary>
    [Fact]
    public async Task ViewsNameEveryControlAndRespectReducedMotion()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var projects = new ProjectsView();
            var memory = new MemoryView();
            var instructions = new InstructionsView();
            projects.ApplyMotion(true);
            memory.ApplyMotion(true);
            instructions.ApplyMotion(true);
            projects.Classes.Contains(ProjectsView.ReducedMotionClass).ShouldBeTrue();
            memory.Classes.Contains(MemoryView.ReducedMotionClass).ShouldBeTrue();
            instructions.Classes.Contains(InstructionsView.ReducedMotionClass).ShouldBeTrue();

            AssertNamedInOrder(projects.KeyboardOrder, ["Project list", "Project name", "Rename project", "Archive project", "Delete project", "Confirm delete"]);
            AssertNamedInOrder(memory.KeyboardOrder, ["Memory entries", "Delete memory entry", "Memory enabled", "Memory paused", "Include sensitive"]);
            AssertNamedInOrder(instructions.KeyboardOrder, ["Global instructions editor", "Load instructions", "Save instructions"]);
            AutomationProperties.GetName(instructions).ShouldBe("Global instructions");
            instructions.FindControl<TextBlock>("LimitText")!.Text.ShouldBe(GlobalInstructionsModel.LimitMessage);
        });

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Autonogrammer.Native.Views/Views/Cowork"));
        foreach (var name in new[] { "ProjectsView.axaml", "MemoryView.axaml", "InstructionsView.axaml" })
        {
            var text = File.ReadAllText(Path.Combine(root, name));
            text.ShouldNotContain("Background=\"#");
            text.ShouldNotContain("Foreground=\"#");
            text.ShouldNotContain("Transition");
            text.ShouldNotContain("Autonogrammer.Color.");
        }

        var rail = File.ReadAllText(Path.Combine(root, "CoworkRailView.axaml"));
        rail.ShouldContain("AutomationProperties.Name=\"Projects\"");
        var automations = File.ReadAllText(Path.GetFullPath(Path.Combine(root, "../Automations/AutomationsPageView.axaml")));
        automations.ShouldContain("AutomationProperties.Name=\"Session targets\"");
        automations.ShouldContain("AutomationProperties.Name=\"Next run\"");
    }

    /// <summary>The rail Projects section opens the editor.</summary>
    [Fact]
    public async Task RailProjectsSectionOpensTheEditor()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var rail = new CoworkRailView();
            rail.ProjectsSection.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            rail.ProjectEditor.Content.ShouldBeOfType<ProjectsView>();
        });
    }

    private static void AssertNamedInOrder(IReadOnlyList<Control> controls, IReadOnlyList<string> names)
    {
        controls.Count.ShouldBe(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            AutomationProperties.GetName(controls[i]).ShouldBe(names[i]);
            controls[i].TabIndex.ShouldBe(i + 1);
        }
    }
}
