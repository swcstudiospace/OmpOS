using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Type-level layer rules over the loaded product assemblies (decision D-02).
/// Complements the csproj-graph tests, which catch edges the compiler drops.
/// Assemblies are passed to the rules as objects: the string overload of
/// ResideInAssembly compares full assembly names, so short names match nothing.
/// </summary>
public sealed class ArchUnitLayerTests
{
    private static readonly IReadOnlyDictionary<string, System.Reflection.Assembly> LoadedAssemblies = LoadProductAssemblies();
    private static readonly Architecture Arch = new ArchLoader().LoadAssemblies(LoadedAssemblies.Values.ToArray()).Build();

    /// <summary>
    /// Namespace of the helper types that the Avalonia XAML compiler generates into every assembly that compiles XAML. Each such
    /// assembly generates its own copy under the same name, so the architecture loader reads the copies as one type and reports an
    /// edge from the shared views library to the desktop head that does not exist. Only these generated types are exempt.
    /// </summary>
    private const string GeneratedXamlNamespace = "CompiledAvaloniaXaml";

    [Fact]
    public void ArchUnit_LowerLayersDoNotDependOnHigher()
    {
        foreach (var from in LayerMap.Layers)
        {
            foreach (var to in LayerMap.Layers.Where(candidate => candidate.Value > from.Value))
            {
                if (!LoadedAssemblies.TryGetValue(from.Key, out var fromAssembly) || !LoadedAssemblies.TryGetValue(to.Key, out var toAssembly))
                {
                    continue;
                }

                Types().That().ResideInAssembly(fromAssembly).And().DoNotHaveFullNameStartingWith(GeneratedXamlNamespace)
                    .Should().NotDependOnAny(Types().That().ResideInAssembly(toAssembly).And().DoNotHaveFullNameStartingWith(GeneratedXamlNamespace))
                    .Because($"{from.Key} (L{from.Value}) must not depend on {to.Key} (L{to.Value})").Check(Arch);
            }
        }
    }

    [Fact]
    public void ArchUnit_NegativeRuleFires()
    {
        // Live-gate proof: this rule MUST be violated (the line below creates a
        // real type dependency from the test assembly to Contracts), so a green
        // suite means the rule engine fires rather than passing vacuously.
        var testAssembly = typeof(ProjectGraphTests).Assembly;
        var contractsAssembly = typeof(OmpOS.Contracts.AssemblyMarker).Assembly;
        var architecture = new ArchLoader().LoadAssemblies(testAssembly, contractsAssembly).Build();
        var rule = Types().That().ResideInAssembly(testAssembly).Should().NotDependOnAny(Types().That().ResideInAssembly(contractsAssembly))
            .Because("negative proof: the test assembly references Contracts, so this rule must fire");
        rule.HasNoViolations(architecture).ShouldBeFalse();
        Should.Throw<FailedArchRuleException>(() => rule.Check(architecture));
    }

    private static Dictionary<string, System.Reflection.Assembly> LoadProductAssemblies()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var assemblies = new Dictionary<string, System.Reflection.Assembly>(StringComparer.Ordinal);
        foreach (var name in LayerMap.Layers.Keys)
        {
            if (ProjectGraphChecker.FindCsprojPath(repoRoot, name) is null)
            {
                continue;
            }

            var candidate = Path.Combine(AppContext.BaseDirectory, name + ".dll");
            if (!File.Exists(candidate))
            {
                // Only the Android head may be absent: it targets net10.0-android, which the net10.0 test projects cannot reference,
                // so it is built only in the Android CI job and never in the Linux test output.
                if (!string.Equals(name, "OmpOS.Android", StringComparison.Ordinal))
                {
                    throw new FileNotFoundException($"Expected built assembly for {name} at {candidate}.");
                }

                continue;
            }

            assemblies[name] = System.Reflection.Assembly.LoadFrom(candidate);
        }

        return assemblies;
    }
}
