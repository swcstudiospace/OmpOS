using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Reduced-motion gates for the UI heads (phase 25.7, M-22): a head never builds the shared main view without a motion
/// preference, and a head that declares a transition in its application styles also declares the reduced-motion override
/// that removes it.
/// </summary>
public sealed class HeadMotionTests
{
    private const string ReducedMotionClass = "reduced-motion";

    [Fact]
    public void NoHeadBuildsTheMainViewWithoutAMotionPreference()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var offenders = new List<string>();
        var sites = 0;
        foreach (var path in HeadFiles(repoRoot, "*.cs"))
        {
            var source = File.ReadAllText(path);
            foreach (var arguments in MainViewConstructions(source))
            {
                sites++;
                if (!HasMotionArgument(arguments))
                {
                    offenders.Add($"{Path.GetRelativePath(repoRoot, path)}: new MainView({arguments.Trim()})");
                }
            }
        }

        sites.ShouldBeGreaterThanOrEqualTo(2, "expected the native and Android heads to build MainView");
        offenders.ShouldBeEmpty("every head must pass a MotionPreference when it builds MainView");
    }

    [Fact]
    public void EveryHeadTransitionHasAReducedMotionOverride()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var checkedHeads = 0;
        foreach (var path in HeadFiles(repoRoot, "*.axaml"))
        {
            var document = XDocument.Load(path);
            if (!document.Descendants().Any(static element => string.Equals(element.Name.LocalName, "Application", StringComparison.Ordinal)))
            {
                continue;
            }

            checkedHeads++;
            UnoverriddenTransitions(document)
                .ShouldBeEmpty($"{Path.GetRelativePath(repoRoot, path)} declares a transition with no reduced-motion override");
        }

        checkedHeads.ShouldBeGreaterThanOrEqualTo(2, "expected the native and Android application styles");
    }

    [Fact]
    public void TheScanReportsATransitionWithNoOverride()
    {
        var document = XDocument.Parse(StylesWith(overrideStyle: string.Empty));

        UnoverriddenTransitions(document).ShouldBe(["Button"]);
    }

    [Fact]
    public void TheScanAcceptsATransitionWithAnEmptyReducedMotionOverride()
    {
        var document = XDocument.Parse(StylesWith(overrideStyle: $"<Style Selector=\".{ReducedMotionClass} Button\"><Setter Property=\"Transitions\"><Transitions /></Setter></Style>"));

        UnoverriddenTransitions(document).ShouldBeEmpty();
    }

    [Fact]
    public void TheScanRejectsAReducedMotionOverrideThatKeepsATransition()
    {
        var document = XDocument.Parse(StylesWith(overrideStyle: $"<Style Selector=\".{ReducedMotionClass} Button\"><Setter Property=\"Transitions\"><Transitions><BrushTransition Property=\"Background\" /></Transitions></Setter></Style>"));

        UnoverriddenTransitions(document).ShouldBe(["Button"]);
    }

    [Fact]
    public void TheMainViewScanReportsABuildWithoutAMotionPreference()
    {
        var arguments = MainViewConstructions("var view = new MainView(bridge, input, root, prompts: null);").ShouldHaveSingleItem();

        HasMotionArgument(arguments).ShouldBeFalse();
        HasMotionArgument(MainViewConstructions("new MainView(bridge, input, root, null, AndroidMotion.Read())").Single()).ShouldBeTrue();
        HasMotionArgument(MainViewConstructions("new MainView()").Single()).ShouldBeFalse();
    }

    private static string StylesWith(string overrideStyle) =>
        "<Application xmlns=\"https://github.com/avaloniaui\"><Application.Styles>"
        + "<Style Selector=\"Button\"><Setter Property=\"Transitions\"><Transitions><BrushTransition Property=\"Background\" Duration=\"0:0:0.120\" /></Transitions></Setter></Style>"
        + overrideStyle
        + "</Application.Styles></Application>";

    /// <summary>The control type of every transition-declaring style that has no later empty reduced-motion override for the same type.</summary>
    private static List<string> UnoverriddenTransitions(XDocument document)
    {
        var styles = document.Descendants().Where(static element => string.Equals(element.Name.LocalName, "Style", StringComparison.Ordinal)).ToList();
        var overridden = styles
            .Where(style => Selector(style).Contains(ReducedMotionClass, StringComparison.Ordinal) && TransitionsSetter(style) is { } setter && !HasTransitions(setter))
            .Select(style => LastType(Selector(style)))
            .ToHashSet(StringComparer.Ordinal);

        return styles
            .Where(style => !Selector(style).Contains(ReducedMotionClass, StringComparison.Ordinal) && TransitionsSetter(style) is { } setter && HasTransitions(setter))
            .Select(style => LastType(Selector(style)))
            .Where(type => !overridden.Contains(type))
            .ToList();
    }

    private static string Selector(XElement style) => (string?)style.Attribute("Selector") ?? string.Empty;

    private static XElement? TransitionsSetter(XElement style) =>
        style.Elements().FirstOrDefault(static element => string.Equals(element.Name.LocalName, "Setter", StringComparison.Ordinal) && string.Equals((string?)element.Attribute("Property"), "Transitions", StringComparison.Ordinal));

    private static bool HasTransitions(XElement setter) =>
        setter.Descendants().Any(static element => element.Name.LocalName.EndsWith("Transition", StringComparison.Ordinal));

    /// <summary>The control type the selector ends on, with any pseudo-class or class suffix removed (<c>Button:pointerover</c> is <c>Button</c>).</summary>
    private static string LastType(string selector)
    {
        var last = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var end = last.IndexOfAny(['.', ':', '#', '[']);
        return end < 0 ? last : last[..end];
    }

    private static IEnumerable<string> HeadFiles(string repoRoot, string pattern)
    {
        var separators = new[] { Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar };
        return Directory.EnumerateFiles(Path.Combine(repoRoot, "apps"), pattern, SearchOption.AllDirectories)
            .Where(path => !separators.Any(separator => path.Contains(separator, StringComparison.Ordinal)))
            .Where(static path => !path.Contains("OmpOS.Web", StringComparison.Ordinal));
    }

    /// <summary>The argument text of every <c>new MainView(...)</c> in the source.</summary>
    private static List<string> MainViewConstructions(string source)
    {
        const string Marker = "new MainView(";
        var found = new List<string>();
        var from = 0;
        while (true)
        {
            var at = source.IndexOf(Marker, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return found;
            }

            var start = at + Marker.Length;
            var depth = 1;
            var end = start;
            while (end < source.Length && depth > 0)
            {
                depth += source[end] switch { '(' => 1, ')' => -1, _ => 0 };
                end++;
            }

            found.Add(source[start..(end - 1)]);
            from = end;
        }
    }

    private static bool HasMotionArgument(string arguments) => arguments.Contains("otion", StringComparison.Ordinal);
}
