using System.Xml.Linq;

using Shouldly;

using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>Only the desktop head references the browser harness, and no relay feed type exists.</summary>
public sealed class BrowserUseIsolationTests
{
    private static readonly string[] ProductSourceRoots = ["src", "apps", "services"];

    private static readonly string[] ForbiddenReferencers =
    [
        "Autonogrammer.Web",
        "Autonogrammer.Api",
        "Autonogrammer.AgentWorker",
        "Autonogrammer.UI",
        "Autonogrammer.Presentation",
        "Autonogrammer.Native.Views",
        "Autonogrammer.Android",
    ];

    /// <summary>Product project edges name Harness.BrowserUse only from Native.</summary>
    [Fact]
    public void OnlyNativeReferencesHarnessBrowserUse()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var from = ProjectGraphChecker.ReadEdges(root)
            .Where(static edge => string.Equals(edge.To, "Autonogrammer.Harness.BrowserUse", StringComparison.Ordinal))
            .Select(static edge => edge.From)
            .ToArray();
        from.ShouldBe(["Autonogrammer.Native"]);
        from.Intersect(ForbiddenReferencers, StringComparer.Ordinal).ShouldBeEmpty();
    }

    /// <summary>Only the browser harness project names PuppeteerSharp.</summary>
    [Fact]
    public void OnlyHarnessBrowserUseNamesPuppeteerSharp()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var named = new List<string>();
        foreach (var project in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            if (project.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var document = XDocument.Load(project);
            var names = document.Descendants().Where(static element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
                .Select(static element => element.Attribute("Include")?.Value)
                .Where(static name => string.Equals(name, "PuppeteerSharp", StringComparison.Ordinal));
            if (names.Any())
            {
                named.Add(Path.GetFileNameWithoutExtension(project));
            }
        }

        named.ShouldBe(["Autonogrammer.Harness.BrowserUse"]);
    }

    /// <summary>No product source declares a relay feed.</summary>
    [Fact]
    public void NoRelayBrowserPaneFeedTypeExists()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        foreach (var source in ProductSourceRoots.SelectMany(relative => Directory.EnumerateFiles(Path.Combine(root, relative), "*.cs", SearchOption.AllDirectories)))
        {
            File.ReadAllText(source).ShouldNotContain("RelayBrowserPaneFeed", Case.Sensitive, source);
        }
    }
}
