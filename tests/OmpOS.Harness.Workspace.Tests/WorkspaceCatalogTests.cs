using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests;

/// <summary>
/// Verifies rule, skill and mode loading, malformed-file diagnostics and reload on change (ADE-17).
/// </summary>
public sealed class WorkspaceCatalogTests
{
    private const string ValidMode = """
        { "id": "review", "displayName": "Review", "rules": ["style"], "skills": ["triage"] }
        """;

    private static readonly string[] StyleOnly = ["style"];
    private static readonly string[] TriageOnly = ["triage"];
    private static readonly string[] ReviewOnly = ["review"];
    private static readonly byte[] InvalidUtf8 = [0xFF, 0xFE, 0x00];

    private static readonly string[] MalformedPaths =
    [
        ".ompos/modes/broken.json",
        ".ompos/modes/unknown-field.json",
        ".ompos/modes/unknown-rule.json",
        ".ompos/rules/Bad Name.md",
        ".ompos/rules/binary.md",
        ".ompos/rules/empty.md",
    ];

    private static readonly (string Id, string Text)[] ReloadedRules =
    [
        ("extra", "extra\n"),
        ("style", "second\n"),
    ];

    [Fact]
    public void LoadsValidRuleSkillAndModeFiles()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use four spaces.\n");
        workspace.Write(".ompos/skills/triage.md", "Label issues by area.\n");
        workspace.Write(".ompos/modes/review.json", ValidMode);

        using var catalog = new WorkspaceCatalog(workspace.Root);
        var snapshot = catalog.Current;

        snapshot.Rules.Select(rule => rule.Id).ToArray().ShouldBe(StyleOnly);
        snapshot.Rules[0].Text.ShouldBe("Use four spaces.\n");
        snapshot.Rules[0].RelativePath.ShouldBe(".ompos/rules/style.md");
        snapshot.Skills.Select(skill => skill.Id).ToArray().ShouldBe(TriageOnly);
        var mode = snapshot.Modes.Single();
        mode.Id.ShouldBe("review");
        mode.DisplayName.ShouldBe("Review");
        mode.Rules.ToArray().ShouldBe(StyleOnly);
        mode.Skills.ToArray().ShouldBe(TriageOnly);
        mode.RelativePath.ShouldBe(".ompos/modes/review.json");
        snapshot.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ReportsEachMalformedFileOnceAndLoadsTheRest()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use four spaces.\n");
        workspace.Write(".ompos/rules/empty.md", "   \n");
        workspace.WriteBytes(".ompos/rules/binary.md", InvalidUtf8);
        workspace.Write(".ompos/rules/Bad Name.md", "Not a valid id.\n");
        workspace.Write(".ompos/skills/triage.md", "Label issues by area.\n");
        workspace.Write(".ompos/modes/review.json", ValidMode);
        workspace.Write(".ompos/modes/broken.json", "{ \"id\": \"broken\"");
        workspace.Write(".ompos/modes/unknown-rule.json", "{ \"id\": \"ghost\", \"displayName\": \"Ghost\", \"rules\": [\"missing\"] }");
        workspace.Write(".ompos/modes/unknown-field.json", "{ \"id\": \"extra\", \"displayName\": \"Extra\", \"colour\": \"red\" }");

        using var catalog = new WorkspaceCatalog(workspace.Root);
        var snapshot = catalog.Current;

        snapshot.Rules.Select(rule => rule.Id).ToArray().ShouldBe(StyleOnly);
        snapshot.Modes.Select(mode => mode.Id).ToArray().ShouldBe(ReviewOnly);
        snapshot.Diagnostics.Select(diagnostic => diagnostic.RelativePath).ToArray().ShouldBe(MalformedPaths);
        snapshot.Diagnostics.ShouldAllBe(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Reason));
    }

    [Fact]
    public void ReloadPicksUpEditedAddedAndRemovedFiles()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "first\n");
        using var catalog = new WorkspaceCatalog(workspace.Root);

        workspace.Write(".ompos/rules/style.md", "second\n");
        workspace.Write(".ompos/rules/extra.md", "extra\n");
        var edited = catalog.Reload();

        edited.ShouldBeSameAs(catalog.Current);
        edited.Rules.Select(rule => (rule.Id, rule.Text)).ToArray().ShouldBe(ReloadedRules);

        File.Delete(Path.Combine(workspace.RootPath, ".ompos", "rules", "extra.md"));
        catalog.Reload().Rules.Select(rule => rule.Id).ToArray().ShouldBe(StyleOnly);
    }

    [Fact]
    public async Task WatcherPublishesChangedSnapshotAfterEdit()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "first\n");
        using var catalog = new WorkspaceCatalog(workspace.Root);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Changed += (_, _) =>
        {
            if (catalog.Current.Rules.Any(rule => string.Equals(rule.Text, "watched\n", StringComparison.Ordinal)))
            {
                published.TrySetResult();
            }
        };

        workspace.Write(".ompos/rules/style.md", "watched\n");

        await published.Task
            .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        catalog.Current.Rules.Single().Text.ShouldBe("watched\n");
    }
}
