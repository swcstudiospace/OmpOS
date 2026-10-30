using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Workspace.BuiltinSkills;
using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The built-in skills of the application live on the machine that runs the head, so the API never reports them as workspace conventions.</summary>
public sealed class ConventionsMapperTests
{
    /// <summary>A built-in skill, and the note that a project skill replaced one, are left out of the response; the project skill stays.</summary>
    [Fact]
    public async Task ToResponse_LeavesTheBuiltinSkillsOut()
    {
        using var bundle = new TempWorkspace();
        bundle.Write("skills/review/SKILL.md", "---\ndescription: Built-in review.\n---\nSteps.\n");
        bundle.Write("skills/audit/SKILL.md", "---\ndescription: Built-in audit.\n---\nSteps.\n");
        using var workspace = new TempWorkspace();
        workspace.Write(".cursor/skills/review/SKILL.md", "---\ndescription: Our review.\n---\nOurs.\n");
        var roots = new[] { workspace.Root };
        var sources = ConventionSources.Local(roots, null).WithBuiltin(new BuiltinSkillSource(bundle.Root));

        var set = await ConventionCatalog.DiscoverAsync(roots, null, sources, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        set.Skills.Select(static skill => skill.Scope).ShouldContain(ConventionScope.Builtin, "the discovery itself found the built-in audit skill");
        set.Diagnostics.ShouldContain(static diagnostic => diagnostic.Kind == ConventionDiagnosticKind.Duplicate);

        var response = ConventionsMapper.ToResponse(set, workspace.Root, null);

        var skill = response.Skills.ShouldHaveSingleItem();
        skill.Name.ShouldBe("review");
        skill.Scope.ShouldBe(ConventionScopeKind.Project);
        skill.Description.ShouldBe("Our review.");
        response.Diagnostics.ShouldBeEmpty();
    }
}
