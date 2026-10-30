using System.Xml.Linq;

using Shouldly;

using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Structural choke point. 73-03 appends Tools, Frames and Audit composition only through a separate ComputerUseToolsComposition file.
/// </summary>
public sealed class ComputerUseBoundaryTests
{
    private static readonly string[] BackendAllowList =
    [
        "IComputerUseBackend.cs",
        "ComputerActionExecutor.cs",
        "KillSwitchCoordinator.cs",
        "ComputerUseComposition.cs",
        "FakeComputerUseBackend.cs",
        "NullComputerUseBackend.cs",
        "X11Backend.cs",
        "WindowsBackend.cs",
        "MacOSBackend.cs",
        "ComputerUseHeadComposition.cs",
        "ComputerStatusAdapter.cs",
    ];

    [Fact]
    public void InjectAsyncIsReferencedByTheExecutorOnly()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        List<string> callers = ComputerUseBoundaryChecker.FindInjectCallers(ReadSources(root));
        callers.ShouldBeEmpty();
    }

    [Fact]
    public void BackendReferencesStayOnTheAllowList()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        List<string> offenders = ComputerUseBoundaryChecker.FindBackendReferences(ReadSources(root), BackendAllowList);
        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void SyntheticSecondInjectCallerAndWebEdgeAreFlagged()
    {
        List<string> callers = ComputerUseBoundaryChecker.FindInjectCallers(
        [
            ("ComputerActionExecutor.cs", "await backend.InjectAsync(primitive, cancellationToken);"),
            ("RogueTool.cs", "await backend.InjectAsync(primitive, cancellationToken);"),
        ]);
        callers.ShouldContain(static item => item.Contains("RogueTool.cs", StringComparison.Ordinal));
        callers.ShouldNotContain(static item => item.Contains("ComputerActionExecutor.cs", StringComparison.Ordinal));

        List<string> edges = ComputerUseBoundaryChecker.FindForbiddenProjectEdges(
        [
            ("Autonogrammer.Web", "Autonogrammer.Harness.ComputerUse"),
            ("Autonogrammer.Presentation", "Autonogrammer.Harness.ComputerUse.Abstractions"),
        ]);
        edges.ShouldContain(static item => item.Contains("Autonogrammer.Web", StringComparison.Ordinal));
        edges.ShouldNotContain(static item => item.Contains("Autonogrammer.Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void AbstractionsHasNoProcessTypeAndNoPackageReference()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        string project = Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions");
        XDocument document = XDocument.Load(Path.Combine(project, "Autonogrammer.Harness.ComputerUse.Abstractions.csproj"));
        document.Descendants("PackageReference").ShouldBeEmpty();
        foreach (string file in Directory.EnumerateFiles(project, "*.cs"))
        {
            string text = StripComments(File.ReadAllText(file));
            text.ShouldNotContain("System.Diagnostics.Process");
            text.ShouldNotContain("new Process");
        }
    }

    [Fact]
    public void ComputerUseProjectDoesNotReferenceAnOsBackend()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        string csproj = File.ReadAllText(Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse", "Autonogrammer.Harness.ComputerUse.csproj"));
        csproj.ShouldNotContain("ComputerUse.X11");
        csproj.ShouldNotContain("ComputerUse.Windows");
        csproj.ShouldNotContain("ComputerUse.MacOS");
    }

    [Fact]
    public void WebUiApiWorkerAndAndroidDoNotReferenceComputerUseProjects()
    {
        string root = ProjectGraphChecker.FindRepoRoot();
        List<string> violations = ComputerUseBoundaryChecker.FindForbiddenProjectEdges(ProjectGraphChecker.ReadEdges(root));
        violations.ShouldBeEmpty();
    }

    private static List<(string Name, string Source)> ReadSources(string root)
    {
        List<(string Name, string Source)> sources = [];
        foreach (string tree in new[] { "src", "apps", "services" })
        {
            string path = Path.Combine(root, tree);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                sources.Add((Path.GetFileName(file), StripComments(File.ReadAllText(file))));
            }
        }

        return sources;
    }

    private static string StripComments(string source)
    {
        System.Text.StringBuilder builder = new();
        foreach (string line in source.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}

/// <summary>Pure checkers so a synthetic violation can be asserted without editing the tree.</summary>
internal static class ComputerUseBoundaryChecker
{
    private static readonly string[] IsolatedHeads =
    [
        "Autonogrammer.Web",
        "Autonogrammer.UI",
        "Autonogrammer.Api",
        "Autonogrammer.AgentWorker",
        "Autonogrammer.Android",
    ];

    public static List<string> FindInjectCallers(IEnumerable<(string Name, string Source)> sources)
    {
        List<string> callers = [];
        foreach ((string name, string source) in sources)
        {
            if (name == "ComputerActionExecutor.cs")
            {
                continue;
            }

            if (source.Contains(".InjectAsync(", StringComparison.Ordinal))
            {
                callers.Add(name);
            }
        }

        return callers;
    }

    public static List<string> FindBackendReferences(IEnumerable<(string Name, string Source)> sources, IReadOnlyList<string> allowList)
    {
        List<string> offenders = [];
        foreach ((string name, string source) in sources)
        {
            if (allowList.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            if (source.Contains("IComputerUseBackend", StringComparison.Ordinal))
            {
                offenders.Add(name);
            }
        }

        return offenders;
    }

    public static List<string> FindForbiddenProjectEdges(IEnumerable<(string From, string To)> edges)
    {
        List<string> violations = [];
        foreach ((string from, string to) in edges)
        {
            if (!to.StartsWith("Autonogrammer.Harness.ComputerUse", StringComparison.Ordinal))
            {
                continue;
            }

            if (IsolatedHeads.Contains(from, StringComparer.Ordinal))
            {
                violations.Add(from + " -> " + to);
            }

            if (from == "Autonogrammer.Presentation" && to != "Autonogrammer.Harness.ComputerUse.Abstractions")
            {
                violations.Add(from + " -> " + to);
            }
        }

        return violations;
    }
}
