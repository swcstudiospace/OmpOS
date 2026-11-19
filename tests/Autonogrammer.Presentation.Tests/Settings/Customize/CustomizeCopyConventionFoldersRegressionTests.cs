using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Customize;

/// <summary>The Rules and Skills empty states name the folders discovery scans, taken from <see cref="ConventionDirectories.Names"/>.</summary>
public sealed class CustomizeCopyConventionFoldersRegressionTests
{
    private static string FolderList()
    {
        var names = ConventionDirectories.Names;
        return string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1];
    }

    /// <summary>The discovered folders appear in precedence order in both empty states.</summary>
    [Fact]
    public void EmptyStatesNameEveryDiscoveredFolderInPrecedenceOrder()
    {
        foreach (var text in new[] { CustomizeCopy.RulesEmpty, CustomizeCopy.SkillsEmpty })
        {
            var position = -1;
            foreach (var folder in ConventionDirectories.Names)
            {
                var found = text.IndexOf(folder, StringComparison.Ordinal);
                found.ShouldBeGreaterThan(position);
                position = found;
            }
        }
    }

    /// <summary>The folder list in both empty states is the one discovery reads.</summary>
    [Fact]
    public void EmptyStatesUseTheConventionFolderList()
    {
        CustomizeCopy.RulesEmpty.ShouldContain($"inside {FolderList()} (in the workspace");
        CustomizeCopy.SkillsEmpty.ShouldContain($"inside {FolderList()} (in the workspace");
    }
}
