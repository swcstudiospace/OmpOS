using System.Globalization;
using System.Reflection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// Phase 21 parity inventory guard. Every session-chat row in <c>docs/parity/session-chat-parity.md</c> must name at
/// least one Razor test, and every named Razor test must exist by name in the Razor test assembly
/// (<c>Autonogrammer.UI.Tests</c>). Every row must also name at least one Avalonia test or state why it is pending, and every
/// named Avalonia test must exist by name in the native test assembly (<c>Autonogrammer.Native.Tests</c>). Both assemblies are
/// loaded from their build output; this project does not reference them.
/// </summary>
public sealed class ParityInventoryTests
{
    private const string InventoryRelativePath = "docs/parity/session-chat-parity.md";
    private const string RazorTestProjectName = "Autonogrammer.UI.Tests";
    private const string RazorTestsHeader = "Razor tests";
    private const string AvaloniaTestProjectName = "Autonogrammer.Native.Tests";
    private const string AvaloniaTestsHeader = "Avalonia test";
    private const string PendingPrefix = "pending:";

    /// <summary>Every session-chat row names at least one covering Razor test.</summary>
    [Fact]
    public void EveryInventoryRowNamesARazorTest()
    {
        var rows = ReadInventoryRows();

        rows.ShouldNotBeEmpty("the inventory must contain session-chat rows");
        var rowsWithoutTests = rows
            .Where(row => row.RazorTests.Count == 0)
            .Select(row => $"row {row.Number} ({row.Feature})")
            .ToArray();
        rowsWithoutTests.ShouldBeEmpty("every session-chat row must name a covering Razor test");
    }

    /// <summary>Every Razor test named in the inventory exists by name in the Razor test assembly.</summary>
    [Fact]
    public void EveryNamedRazorTestExistsInTheRazorTestAssembly()
    {
        var rows = ReadInventoryRows();
        var razorTests = LoadTestNames(RazorTestProjectName);

        var unresolved = rows
            .SelectMany(row => row.RazorTests.Select(name => (row.Number, name)))
            .Where(entry => !IsClassMethodName(entry.name) || !razorTests.Contains(entry.name))
            .Select(entry => $"row {entry.Number}: '{entry.name}' is not a Razor test in {RazorTestProjectName}")
            .ToArray();

        unresolved.ShouldBeEmpty("every Razor test named in the inventory must exist in the Razor test assembly");
    }

    /// <summary>Every session-chat row names an Avalonia test, or states why its Avalonia coverage is pending.</summary>
    [Fact]
    public void EveryInventoryRowStatesAnAvaloniaTestOrAPendingReason()
    {
        var rows = ReadInventoryRows();

        rows.ShouldNotBeEmpty("the inventory must contain session-chat rows");
        var rowsWithoutCoverage = rows
            .Where(row => row.Avalonia.Tests.Count == 0 && string.IsNullOrEmpty(row.Avalonia.PendingReason))
            .Select(row => $"row {row.Number} ({row.Feature})")
            .ToArray();
        rowsWithoutCoverage.ShouldBeEmpty("every session-chat row must name a covering Avalonia test or state why it is pending");
    }

    /// <summary>Every Avalonia test named in the inventory exists by name in the native test assembly.</summary>
    [Fact]
    public void EveryNamedAvaloniaTestExistsInTheNativeTestAssembly()
    {
        var rows = ReadInventoryRows();
        var nativeTests = LoadTestNames(AvaloniaTestProjectName);

        var unresolved = rows
            .SelectMany(row => row.Avalonia.Tests.Select(name => (row.Number, name)))
            .Where(entry => !IsClassMethodName(entry.name) || !nativeTests.Contains(entry.name))
            .Select(entry => $"row {entry.Number}: '{entry.name}' is not a [Fact] or [Theory] in {AvaloniaTestProjectName}")
            .ToArray();

        unresolved.ShouldBeEmpty("every Avalonia test named in the inventory must exist in the native test assembly");
    }

    private static List<InventoryRow> ReadInventoryRows()
    {
        var path = Path.Combine(FindRepositoryRoot(), InventoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).ShouldBeTrue($"The parity inventory is missing at {path}.");

        var lines = File.ReadAllLines(path);
        var header = lines
            .Select(SplitCells)
            .FirstOrDefault(cells => cells.Contains(RazorTestsHeader, StringComparer.Ordinal))
            ?? throw new InvalidOperationException($"The inventory table must have a '{RazorTestsHeader}' column.");
        var razorColumn = Array.IndexOf(header, RazorTestsHeader);
        var avaloniaColumn = Array.IndexOf(header, AvaloniaTestsHeader);
        avaloniaColumn.ShouldBeGreaterThanOrEqualTo(0, $"The inventory table must have an '{AvaloniaTestsHeader}' column.");

        var rows = new List<InventoryRow>();
        foreach (var line in lines)
        {
            var cells = SplitCells(line);
            if (cells.Length == 0 || !int.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }

            cells.Length.ShouldBe(header.Length, $"Inventory row {number} has the wrong number of cells.");
            var names = cells[razorColumn]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows.Add(new InventoryRow(number, cells[1], names, ParseAvaloniaCell(cells[avaloniaColumn])));
        }

        return rows;
    }

    private static string[] SplitCells(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|') || trimmed.Length < 2)
        {
            return [];
        }

        return trimmed[1..^1].Split('|', StringSplitOptions.TrimEntries);
    }

    private static AvaloniaCoverage ParseAvaloniaCell(string cell)
    {
        if (cell.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new AvaloniaCoverage([], cell[PendingPrefix.Length..].Trim());
        }

        var names = cell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AvaloniaCoverage(names, null);
    }

    private static bool IsClassMethodName(string name)
    {
        var parts = name.Split('.');
        return parts.Length == 2 && parts.All(IsIdentifier);
    }

    private static bool IsIdentifier(string part) =>
        part.Length > 0
        && (char.IsAsciiLetter(part[0]) || part[0] == '_')
        && part.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static HashSet<string> LoadTestNames(string projectName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var targetFramework = Path.GetFileName(outputDirectory);
        var configuration = Path.GetFileName(Path.GetDirectoryName(outputDirectory) ?? string.Empty);
        var assemblyPath = Path.Combine(
            repositoryRoot, "tests", projectName, "bin", configuration, targetFramework, projectName + ".dll");

        File.Exists(assemblyPath).ShouldBeTrue(
            $"The test assembly is missing at {assemblyPath}. Build tests/{projectName} with configuration '{configuration}' and framework '{targetFramework}' first.");

        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
        ResolveEventHandler resolver = (_, args) =>
        {
            var candidate = Path.Combine(assemblyDirectory, new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in LoadTypes(assembly))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (IsTestMethod(method))
                    {
                        names.Add($"{type.Name}.{method.Name}");
                    }
                }
            }

            names.ShouldNotBeEmpty($"No [Fact] or [Theory] methods were found in {assemblyPath}.");
            return names;
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static Type[] LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }

    private static bool IsTestMethod(MethodInfo method) =>
        method.CustomAttributes.Any(attribute => attribute.AttributeType.FullName is "Xunit.FactAttribute" or "Xunit.TheoryAttribute");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"The repository root (Autonogrammer.slnx) was not found above {AppContext.BaseDirectory}.");
    }

    private sealed record InventoryRow(int Number, string Feature, IReadOnlyList<string> RazorTests, AvaloniaCoverage Avalonia);

    private sealed record AvaloniaCoverage(IReadOnlyList<string> Tests, string? PendingReason);
}
