using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Static gates for the web head's HTML shell and its stylesheet (phase 25.7, M-26): the error banner announces, its dismiss is a
/// named button with a visible focus style, and every transition in the head's own stylesheet is switched off under reduced motion.
/// The web smoke checks the same behaviour in a browser; these gates need no browser.
/// </summary>
public sealed partial class WebShellAccessibilityTests
{
    [Fact]
    public void ErrorBannerIsAnAlertWithANamedDismissButton()
    {
        var html = ReadWebFile("index.html");

        var banner = BannerRegex().Match(html);
        banner.Success.ShouldBeTrue("index.html must carry #blazor-error-ui");
        banner.Groups["attributes"].Value.ShouldContain("role=\"alert\"", Case.Sensitive, "the banner must announce when Blazor shows it");

        var dismiss = DismissRegex().Match(banner.Groups["body"].Value);
        dismiss.Success.ShouldBeTrue("the banner must carry a <button class=\"dismiss\"> as its dismiss control");
        dismiss.Groups["attributes"].Value.ShouldContain("type=\"button\"", Case.Sensitive);
        AriaLabelRegex().Match(dismiss.Groups["attributes"].Value).Groups["name"].Value.Trim()
            .ShouldNotBeEmpty("the dismiss button needs an accessible name");
    }

    [Fact]
    public void ErrorBannerDismissIsNotAnInertElement()
    {
        var html = ReadWebFile("index.html");

        InertDismissRegex().IsMatch(html).ShouldBeFalse("a span or div dismiss has no role, name or keyboard path");
    }

    [Fact]
    public void ErrorBannerDismissHasAVisibleFocusStyle()
    {
        var css = ReadWebFile(Path.Combine("css", "app.css"));

        var focus = FocusRuleRegex().Match(css);
        focus.Success.ShouldBeTrue("app.css must style #blazor-error-ui .dismiss:focus-visible");
        focus.Groups["body"].Value.ShouldContain("outline:", Case.Sensitive);
        focus.Groups["body"].Value.ShouldNotContain("outline: none", Case.Insensitive);
    }

    [Fact]
    public void EveryTransitionInTheHeadStylesheetIsDisabledUnderReducedMotion()
    {
        var css = ReadWebFile(Path.Combine("css", "app.css"));

        var reduced = ReducedMotionRegex().Matches(css).Select(static match => match.Groups["body"].Value).ToList();
        var transitionSelectors = TransitionRuleRegex().Matches(css).Select(static match => match.Groups["selector"].Value.Trim()).ToList();

        transitionSelectors.ShouldNotBeEmpty("the loading ring transition is expected in app.css");
        foreach (var selector in transitionSelectors)
        {
            reduced.ShouldContain(
                body => body.Contains(selector, StringComparison.Ordinal) && body.Contains("transition: none", StringComparison.Ordinal),
                $"{selector} declares a transition and needs a prefers-reduced-motion override");
        }
    }

    private static string ReadWebFile(string relative)
    {
        var path = Path.Combine(ProjectGraphChecker.FindRepoRoot(), "apps", "Autonogrammer.Web", "wwwroot", relative);
        File.Exists(path).ShouldBeTrue($"expected {path}");
        return File.ReadAllText(path);
    }

    [GeneratedRegex("<div(?<attributes>[^>]*\\bid=\"blazor-error-ui\"[^>]*)>(?<body>.*?)</div>", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BannerRegex();

    [GeneratedRegex("<button(?<attributes>[^>]*\\bclass=\"[^\"]*\\bdismiss\\b[^\"]*\"[^>]*)>", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DismissRegex();

    [GeneratedRegex("aria-label=\"(?<name>[^\"]*)\"", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AriaLabelRegex();

    [GeneratedRegex("<(span|div|a)\\b[^>]*\\bclass=\"[^\"]*\\bdismiss\\b", RegexOptions.None | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex InertDismissRegex();

    [GeneratedRegex("#blazor-error-ui\\s+\\.dismiss:focus-visible\\s*\\{(?<body>[^}]*)\\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FocusRuleRegex();

    [GeneratedRegex("@media\\s*\\(prefers-reduced-motion:\\s*reduce\\)\\s*\\{(?<body>.*?\\n\\})", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ReducedMotionRegex();

    [GeneratedRegex("(?<selector>[^{}@]+)\\{[^{}]*\\btransition\\s*:\\s*(?!none)[^;}]+;", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TransitionRuleRegex();
}
