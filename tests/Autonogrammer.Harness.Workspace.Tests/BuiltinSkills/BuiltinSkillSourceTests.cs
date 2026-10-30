using Autonogrammer.Workspace.BuiltinSkills;
using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.BuiltinSkills;

/// <summary>The bundled skills folder is read like any convention source, stays inside itself and is silent when it is not there.</summary>
public sealed class BuiltinSkillSourceTests
{
    /// <summary>A base directory without a <c>skills</c> folder lists nothing and reports no problem: a head that does not bundle the skills is not broken.</summary>
    [Fact]
    public async Task AMissingFolderListsNothingAndIsNotAProblem()
    {
        using var workspace = new TempWorkspace();
        var source = new BuiltinSkillSource(Path.Combine(workspace.RootPath, "absent"));

        var list = await source.ListAsync(source.SkillsPath, 100, TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.FolderExists.ShouldBeFalse();
        list.Problem.ShouldBeNull();
        list.Entries.ShouldBeEmpty();
    }

    /// <summary>The source lists the skill folders in ordinal order and reads their <c>SKILL.md</c>.</summary>
    [Fact]
    public async Task ListsAndReadsTheSkillsOfTheFolder()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("skills/review/SKILL.md", "---\nname: review\n---\nReview.\n");
        workspace.Write("skills/audit/SKILL.md", "Audit.\n");
        var source = new BuiltinSkillSource(workspace.RootPath);

        var list = await source.ListAsync(source.SkillsPath, 100, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var read = await source.ReadTextAsync(Path.Combine(source.SkillsPath, "review", "SKILL.md"), 1024, TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.FolderExists.ShouldBeTrue();
        list.Entries.Select(entry => (entry.Name, entry.IsDirectory)).ShouldBe([("audit", true), ("review", true)]);
        read.Text.ShouldBe("---\nname: review\n---\nReview.\n");
    }

    /// <summary>A file next to the <c>skills</c> folder is outside the source and is refused, so the bundled folder cannot be used to read the rest of the install.</summary>
    [Fact]
    public async Task AFileOutsideTheSkillsFolderIsRefused()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("skills/review/SKILL.md", "Review.\n");
        workspace.Write("appsettings.json", "{}");
        var source = new BuiltinSkillSource(workspace.RootPath);

        var read = await source.ReadTextAsync(Path.Combine(workspace.RootPath, "appsettings.json"), 1024, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var exists = await source.ExistsAsync(Path.Combine(workspace.RootPath, "appsettings.json"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        read.IsRead.ShouldBeFalse();
        read.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
        exists.ShouldBeFalse();
    }

    /// <summary>The default source is the <c>skills</c> folder in the application's base directory.</summary>
    [Fact]
    public void TheDefaultSourceIsNextToTheApplication()
    {
        var source = BuiltinSkillSource.CreateDefault();

        source.SkillsPath.ShouldBe(Path.Combine(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), "skills"));
    }

    /// <summary>A blank base directory is a programming error.</summary>
    [Fact]
    public void ABlankBaseDirectoryIsRejected()
    {
        _ = Should.Throw<ArgumentException>(() => new BuiltinSkillSource(" "));
    }
}
