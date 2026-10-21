using System.Xml.Linq;
using OmpOS.Presentation.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Verifies that the presentation layer stays free of UI frameworks (XP-08, ROADMAP criterion 2, project half):
/// no Avalonia, MAUI or ASP.NET Core Components reference in the project file or the built assembly, and no
/// public type that derives from or implements a UI-framework type.
/// </summary>
public sealed class PresentationIsolationTests
{
    private static readonly string[] UiFrameworkPrefixes = ["Avalonia", "Microsoft.Maui", "Microsoft.AspNetCore.Components"];

    /// <summary>XP-08: the presentation project file declares no UI-framework PackageReference or ProjectReference.</summary>
    [Fact]
    public void ProjectFile_DeclaresNoUiFrameworkReference()
    {
        var projectPath = Path.Combine(FindRepoRoot(), "src", "OmpOS.Presentation", "OmpOS.Presentation.csproj");
        var document = XDocument.Load(projectPath);

        var referenceNames = document.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => ReferenceName(element.Attribute("Include")?.Value ?? string.Empty))
            .ToArray();

        referenceNames.ShouldNotBeEmpty();
        referenceNames.Where(IsUiFrameworkName).ShouldBeEmpty("OmpOS.Presentation must not reference a UI framework");
    }

    /// <summary>XP-08: the built presentation assembly references no UI-framework assembly.</summary>
    [Fact]
    public void BuiltAssembly_ReferencesNoUiFrameworkAssembly()
    {
        var referenced = typeof(PluginHost).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        referenced.Where(IsUiFrameworkName).ShouldBeEmpty("OmpOS.Presentation must not reference a UI framework assembly");
    }

    /// <summary>XP-08: no public presentation type derives from or implements a UI-framework type.</summary>
    [Fact]
    public void NoPublicType_DerivesFromUiFrameworkType()
    {
        var offenders = typeof(PluginHost).Assembly.GetExportedTypes()
            .Where(DerivesFromUiFramework)
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        offenders.ShouldBeEmpty("public presentation types must not derive from UI-framework types");
    }

    private static bool DerivesFromUiFramework(Type type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (IsUiFrameworkName(baseType.Assembly.GetName().Name))
            {
                return true;
            }
        }

        return type.GetInterfaces().Any(implemented => IsUiFrameworkName(implemented.Assembly.GetName().Name));
    }

    private static string ReferenceName(string include)
    {
        // ProjectReference names a .csproj path; PackageReference names the package id.
        return include.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(include)
            : include;
    }

    private static bool IsUiFrameworkName(string? assemblyOrPackageName)
    {
        return assemblyOrPackageName is not null
            && UiFrameworkPrefixes.Any(prefix => assemblyOrPackageName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("OmpOS.slnx was not found above " + AppContext.BaseDirectory);
    }
}
