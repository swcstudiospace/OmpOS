using System.Reflection;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The settings copy: every text exists, so a view never has to fall back to a literal.</summary>
public sealed class SettingsCopyTests
{
    /// <summary>Every text constant of <see cref="SettingsCopy"/> is non-empty.</summary>
    [Fact]
    public void NoTextIsEmpty()
    {
        var texts = typeof(SettingsCopy)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, Text: (string?)field.GetRawConstantValue()))
            .ToArray();

        texts.Length.ShouldBeGreaterThan(50);
        foreach (var (name, text) in texts)
        {
            string.IsNullOrWhiteSpace(text).ShouldBeFalse($"{name} must not be empty");
        }
    }

    /// <summary>Every navigation title is distinct, so two pages never read alike.</summary>
    [Fact]
    public void NavigationTitlesAreDistinct()
    {
        string[] titles =
        [
            SettingsCopy.RuntimeTitle,
            SettingsCopy.GeneralTitle,
            SettingsCopy.AgentsTitle,
            SettingsCopy.TabTitle,
            SettingsCopy.PluginsTitle,
        ];

        titles.Distinct(StringComparer.Ordinal).Count().ShouldBe(titles.Length);
    }

    /// <summary>The composed texts name what they are about and carry no empty segments.</summary>
    [Fact]
    public void ComposedTextsAreComplete()
    {
        SettingsCopy.TabHowConfigured(true, "OMPOS_COMPLETION_ENDPOINT").ShouldContain("OMPOS_COMPLETION_ENDPOINT", Case.Sensitive);
        SettingsCopy.TabHowConfigured(false, " ").ShouldContain("outside OmpOS", Case.Sensitive);
        SettingsCopy.NavigationEntryName("Agents", isDirty: false).ShouldBe("Agents");
        SettingsCopy.NavigationEntryName("Agents", isDirty: true).ShouldBe("Agents, unsaved changes");

        var found = new PluginRow("swarm", "Agent Swarm", "Subagent orchestrator", "0.9.2", PluginRow.FoundAvailability, null);
        SettingsCopy.PluginSummary(found).ShouldBe("Agent Swarm: Subagent orchestrator. Found. 0.9.2.");
        var unknown = new PluginRow("gsd", "GSD", "Workflow engine", " ", PluginRow.MissingAvailability, "restore it");
        SettingsCopy.PluginSummary(unknown).ShouldBe("GSD: Workflow engine. Missing. Version unknown. restore it");
    }
}
