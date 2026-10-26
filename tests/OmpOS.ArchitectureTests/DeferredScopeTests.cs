using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Phase 30 criterion 5 guard: the two deferred items (VS Code extension compatibility, and admin, analytics and team controls)
/// ship no stub endpoint, placeholder control or other code identifier. Only the deferral records under docs/ and .planning/ may
/// name them. The needles are assembled at run time so this file does not match its own scan.
/// </summary>
public sealed class DeferredScopeTests
{
    private static readonly string[] Needles =
    [
        "ADE-" + "21",
        "ADE-" + "22",
        "vs" + "code",
    ];

    private static readonly string[] CodeRoots = ["src", "services", "apps", "tests"];

    private static readonly string[] CodeExtensions = [".cs", ".axaml", ".razor", ".csproj"];

    private static readonly string[] BuildOutputSegments = ["bin", "obj", "node_modules"];

    [Fact]
    public void DeferredAdeItemsShipNoCodeIdentifiers()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();

        var offenders = FindOffenders(repoRoot);

        offenders.ShouldBeEmpty(
            "deferred items must not appear in code, views, project files or the OpenAPI document:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void DeferredScan_ReportsPlantedIdentifierInTemporaryTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "ompos-deferred-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src", "Sample"));
            Directory.CreateDirectory(Path.Combine(root, "src", "Sample", "obj"));
            File.WriteAllText(Path.Combine(root, "src", "Sample", "Clean.cs"), "internal sealed class Clean;" + Environment.NewLine);
            File.WriteAllText(Path.Combine(root, "src", "Sample", "Planted.cs"), "// " + Needles[0] + Environment.NewLine);
            File.WriteAllText(Path.Combine(root, "src", "Sample", "obj", "Generated.cs"), "// " + Needles[1] + Environment.NewLine);

            var offenders = FindOffenders(root);

            offenders.ShouldHaveSingleItem();
            offenders[0].ShouldStartWith(Path.Combine("src", "Sample", "Planted.cs") + ":1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Lists each line of a scanned code file that names a deferred identifier, as relative path, line and text.</summary>
    /// <param name="repoRoot">The repository root, or a temporary tree with the same layout.</param>
    /// <returns>One entry per offending line, in path order.</returns>
    private static List<string> FindOffenders(string repoRoot)
    {
        var files = new List<string>();
        foreach (var codeRoot in CodeRoots)
        {
            var directory = Path.Combine(repoRoot, codeRoot);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            files.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => CodeExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => !IsBuildOutput(Path.GetRelativePath(repoRoot, path))));
        }

        var openApi = Path.Combine(repoRoot, "docs", "api", "openapi.json");
        if (File.Exists(openApi))
        {
            files.Add(openApi);
        }

        var offenders = new List<string>();
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (Needles.Any(needle => lines[index].Contains(needle, StringComparison.OrdinalIgnoreCase)))
                {
                    offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return offenders;
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar).Any(segment => BuildOutputSegments.Contains(segment, StringComparer.Ordinal));
}
