using System.Xml.Linq;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Reads the on-disk .csproj reference graph and reports layer violations.
/// This catches edges ArchUnitNET cannot see: the compiler drops an unused
/// ProjectReference from assembly metadata, so a type-level rule passes while
/// the project graph still carries the edge.
/// </summary>
internal static class ProjectGraphChecker
{
    private static readonly string[] SourceRoots = ["src", "services", "apps"];

    /// <summary>
    /// Locates the repository root by walking up from the test output directory
    /// until OmpOS.slnx is found.
    /// </summary>
    public static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate OmpOS.slnx walking up from " + AppContext.BaseDirectory)
            : directory.FullName;
    }

    /// <summary>
    /// Reads every (from, to) ProjectReference edge between product projects.
    /// Test projects under tests/ are excluded; they may reference anything.
    /// </summary>
    public static IReadOnlyList<(string From, string To)> ReadEdges(string repoRoot)
    {
        var edges = new List<(string From, string To)>();
        foreach (var csproj in EnumerateProductProjects(repoRoot))
        {
            var from = Path.GetFileNameWithoutExtension(csproj);
            foreach (var include in XDocument.Load(csproj).Descendants("ProjectReference").Attributes("Include"))
            {
                var target = Path.GetFileNameWithoutExtension(include.Value.Replace('\\', '/'));
                if (!string.IsNullOrEmpty(target))
                {
                    edges.Add((from, target));
                }
            }
        }


        return edges;
    }

    /// <summary>
    /// Returns the assembly names of every product project on disk.
    /// </summary>
    public static IReadOnlyList<string> ReadProjectNames(string repoRoot)
    {
        return EnumerateProductProjects(repoRoot).Select(Path.GetFileNameWithoutExtension).Where(static name => !string.IsNullOrEmpty(name)).Select(static name => name!).ToList();
    }

    /// <summary>
    /// Finds the .csproj for an assembly name, or null when no project exists on disk
    /// (every LayerMap entry has a project since Phase 4 landed the MAUI head).
    /// </summary>
    public static string? FindCsprojPath(string repoRoot, string assemblyName)
    {
        return EnumerateProductProjects(repoRoot).FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), assemblyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reports one violation string per edge that targets a higher layer, targets the
    /// same layer without an allow-list entry, or names an assembly missing from the map.
    /// </summary>
    public static IReadOnlyList<string> FindViolations(IEnumerable<(string From, string To)> edges)
    {
        var violations = new List<string>();
        foreach (var (from, to) in edges)
        {
            if (!LayerMap.Layers.TryGetValue(from, out var fromLayer))
            {
                violations.Add($"{from} is not in LayerMap; adding a project requires a one-line map entry");
                continue;
            }

            if (!LayerMap.Layers.TryGetValue(to, out var toLayer))
            {
                violations.Add($"{from} references {to} which is not in LayerMap; adding a project requires a one-line map entry");
                continue;
            }

            if (toLayer > fromLayer)
            {
                violations.Add($"{from} (L{fromLayer}) must not reference higher-layer {to} (L{toLayer})");
            }
            else if (toLayer == fromLayer && !LayerMap.SameLayerAllowed.Contains((from, to)))
            {
                violations.Add($"{from} (L{fromLayer}) must not reference same-layer {to} (L{toLayer}) without an allow-list entry");
            }
        }

        return violations;
    }

    private static IEnumerable<string> EnumerateProductProjects(string repoRoot)
    {
        return SourceRoots.SelectMany(root => Directory.EnumerateFiles(Path.Combine(repoRoot, root), "*.csproj", SearchOption.AllDirectories));
    }
}
