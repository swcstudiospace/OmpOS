using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Plugin-only frontend boundary (Tc00c FR-1, ADR-0001): the plugin surface (Presentation, UI, Web) does not reference the
/// launcher or config projects that resolve or start the engine binary, its copy carries no engine name in user-visible text,
/// and the engine stage maps to the neutral name "Engine". The desktop heads are composition roots and own engine management;
/// backend projects keep their engine references.
/// </summary>
public sealed class PluginFrontendBoundaryTests
{
    private static readonly string[] PluginSurfaceProjects = ["Autonogrammer.Presentation", "Autonogrammer.UI", "Autonogrammer.Web"];

    private static readonly string[] EngineLauncherOrConfigProjects =
        ["Autonogrammer.Harness.OmpLauncher", "Autonogrammer.Harness.OmpConfig"];

    /// <summary>Copy files whose strings the plugin surface shows: none may name the engine binary.</summary>
    private static readonly string[] CopyFiles =
    [
        "src/Autonogrammer.Presentation/Settings/Models/ModelsCopy.cs",
        "src/Autonogrammer.Presentation/Settings/Models/ProviderListingCopy.cs",
        "src/Autonogrammer.Presentation/Settings/SettingsCopy.cs",
        "src/Autonogrammer.Presentation/About/AboutCopy.cs",
        "src/Autonogrammer.UI/Components/Settings/SettingsWebCopy.cs",
    ];

    /// <summary>Product project edges name the launcher or config only outside the plugin surface.</summary>
    [Fact]
    public void PluginSurfaceHasNoEngineLauncherOrConfigEdges()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var violations = ProjectGraphChecker.ReadEdges(root)
            .Where(edge => PluginSurfaceProjects.Contains(edge.From, StringComparer.Ordinal)
                           && EngineLauncherOrConfigProjects.Contains(edge.To, StringComparer.Ordinal))
            .Select(edge => edge.From + " -> " + edge.To)
            .Order(StringComparer.Ordinal)
            .ToArray();
        violations.ShouldBeEmpty("the plugin surface must not reference the engine launcher or config: " + string.Join(", ", violations));
    }

    /// <summary>Type-level proof: no Presentation or UI type depends on a launcher or config type.</summary>
    [Fact]
    public void PresentationAndUiDoNotDependOnEngineLauncherOrConfig()
    {
        var presentation = LoadAssembly("Autonogrammer.Presentation");
        var ui = LoadAssembly("Autonogrammer.UI");
        var launcher = LoadAssembly("Autonogrammer.Harness.OmpLauncher");
        var config = LoadAssembly("Autonogrammer.Harness.OmpConfig");
        var architecture = new ArchLoader().LoadAssemblies(presentation, ui, launcher, config).Build();

        var surface = Types().That().ResideInAssembly(presentation).Or().ResideInAssembly(ui);
        var backend = Types().That().ResideInAssembly(launcher).Or().ResideInAssembly(config);

        surface.Should().NotDependOnAny(backend)
            .Because("frontend view models and views carry plugin-neutral names only; engine resolution stays backend-only").Check(architecture);
    }

    /// <summary>User-visible strings of the plugin-surface copy files contain no engine binary name.</summary>
    [Fact]
    public void PluginSurfaceCopyCarriesNoEngineName()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var hits = new List<string>();
        foreach (var relative in CopyFiles)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).ShouldBeTrue("expected copy file " + relative);
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var literal in StringLiterals(lines[index]))
                {
                    if (ContainsEngineName(literal))
                    {
                        hits.Add(relative + ":" + (index + 1) + ": " + literal);
                    }
                }
            }
        }

        hits.ShouldBeEmpty("engine binary name in user-visible copy:\n" + string.Join(Environment.NewLine, hits));
    }

    /// <summary>The engine stage displays with the neutral name, and its unavailable sentences name no binary.</summary>
    [Fact]
    public void EngineStageMapsToTheNeutralName()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var projection = File.ReadAllText(Path.Combine(root, "src/Autonogrammer.Presentation/AgentPanel/PipelineProjection.cs".Replace('/', Path.DirectorySeparatorChar)));
        projection.ShouldContain("StageSource.Omp => \"Engine\"", Case.Sensitive);

        foreach (var relative in new[]
                 {
                     "src/Autonogrammer.Presentation/AgentPanel/AgentPanelCopy.cs",
                     "src/Autonogrammer.Presentation/AgentPanel/StageAnnouncer.cs",
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            text.ShouldNotContain("The omp engine", Case.Sensitive, relative);
        }
    }

    private static System.Reflection.Assembly LoadAssembly(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
        File.Exists(path).ShouldBeTrue("expected built assembly " + name + " at " + path);
        return System.Reflection.Assembly.LoadFrom(path);
    }

    private static IEnumerable<string> StringLiterals(string line)
    {
        var start = 0;
        while (start < line.Length)
        {
            var open = line.IndexOf('"', start);
            if (open < 0)
            {
                yield break;
            }

            var cursor = open + 1;
            var escaped = false;
            while (cursor < line.Length)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (line[cursor] == '\\')
                {
                    escaped = true;
                }
                else if (line[cursor] == '"')
                {
                    break;
                }

                cursor++;
            }

            yield return line[open..cursor];
            start = cursor + 1;
        }
    }

    private static bool ContainsEngineName(string literal)
    {
        var cursor = 0;
        while (cursor < literal.Length)
        {
            var index = literal.IndexOf("omp", cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var before = index == 0 ? ' ' : literal[index - 1];
            var after = index + 3 >= literal.Length ? ' ' : literal[index + 3];
            if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
            {
                return true;
            }

            cursor = index + 3;
        }

        return false;
    }
}
