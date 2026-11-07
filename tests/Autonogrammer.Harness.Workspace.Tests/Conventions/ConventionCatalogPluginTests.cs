using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Conventions;

/// <summary>Plugin bundle roots are discovered after built-in skills and never hide a project item of the same name.</summary>
public sealed class ConventionCatalogPluginTests
{
    /// <summary>A bundle root yields its skill, the command <c>p:a</c> and the agent <c>p:x</c> in the plugin scope. A project skill of the same name wins.</summary>
    [Fact]
    public async Task PluginRootsDiscoverQualifiedItemsAndLoseToProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "ag-plugin-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "ws");
        var bundle = Path.Combine(root, "p");
        Directory.CreateDirectory(Path.Combine(project, ".autonogrammer", "skills", "foo"));
        Directory.CreateDirectory(Path.Combine(bundle, "skills", "foo"));
        Directory.CreateDirectory(Path.Combine(bundle, "skills", "bar"));
        Directory.CreateDirectory(Path.Combine(bundle, "commands"));
        Directory.CreateDirectory(Path.Combine(bundle, "agents"));
        await File.WriteAllTextAsync(Path.Combine(project, ".autonogrammer", "skills", "foo", "SKILL.md"), "---\nname: foo\ndescription: Project skill.\n---\n\n# foo\n", ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(bundle, "skills", "foo", "SKILL.md"), "---\nname: foo\ndescription: Bundle skill.\n---\n\n# foo\n", ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(bundle, "skills", "bar", "SKILL.md"), "---\nname: bar\ndescription: Bundle only.\n---\n\n# bar\n", ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(bundle, "commands", "a.md"), "---\ndescription: Command a.\n---\n\nDo a.\n", ct).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(bundle, "agents", "x.md"), "---\ndescription: Agent x.\n---\n\nBe x.\n", ct).ConfigureAwait(true);
        try
        {
            var withPlugin = await ConventionCatalog.DiscoverAsync(
                [project],
                null,
                ConventionSources.Local([project], null).WithPluginRoots([bundle]),
                cancellationToken: ct).ConfigureAwait(true);

            withPlugin.Skills.Single(skill => string.Equals(skill.Name, "foo", StringComparison.Ordinal)).Scope.ShouldBe(ConventionScope.Project);
            withPlugin.Skills.ShouldContain(skill => string.Equals(skill.Name, "bar", StringComparison.Ordinal) && skill.Scope == ConventionScope.Plugin);
            withPlugin.Diagnostics.ShouldContain(item => item.Kind == ConventionDiagnosticKind.Duplicate && item.Reason.Contains("'foo'", StringComparison.Ordinal));
            withPlugin.Commands.Single().Name.ShouldBe("p:a");
            withPlugin.Commands.Single().Scope.ShouldBe(ConventionScope.Plugin);
            withPlugin.Subagents.Single().Name.ShouldBe("p:x");
            withPlugin.Subagents.Single().Scope.ShouldBe(ConventionScope.Plugin);

            var without = await ConventionCatalog.DiscoverAsync(
                [project],
                null,
                ConventionSources.Local([project], null),
                cancellationToken: ct).ConfigureAwait(true);
            without.Skills.Select(skill => skill.Name).ShouldBe(["foo"]);
            without.Commands.ShouldBeEmpty();
            without.Subagents.ShouldBeEmpty();
            without.Diagnostics.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
