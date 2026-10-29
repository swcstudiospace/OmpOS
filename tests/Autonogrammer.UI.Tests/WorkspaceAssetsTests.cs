using System.Globalization;
using System.Security.Cryptography;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// The shipped assets of the workspace screens: the vendored xterm.js is exactly what its record says and is loaded from the app's own origin, and the
/// Kanban, Settings and terminal stylesheets keep the layout rules that stop a cut-off column and a cramped dialog (PX-09).
/// </summary>
public sealed class WorkspaceAssetsTests
{
    private static string XtermDirectory => Path.Combine(CssSources.RepoRoot, "src", "Autonogrammer.UI", "wwwroot", "lib", "xterm");

    private static string ScriptsDirectory => Path.Combine(CssSources.RepoRoot, "src", "Autonogrammer.UI", "wwwroot", "scripts");

    /// <summary>VERSION.txt names xterm 6.0.0 under the MIT licence and its sha256 lines match the vendored files byte for byte, so a changed file cannot pass as the recorded one.</summary>
    [Fact]
    public void VendoredXtermMatchesItsRecordedVersionAndDigests()
    {
        var record = File.ReadAllText(Path.Combine(XtermDirectory, "VERSION.txt"));
        record.ShouldContain("package: @xterm/xterm", Case.Sensitive);
        record.ShouldContain("version: 6.0.0", Case.Sensitive);
        record.ShouldContain("licence: MIT", Case.Sensitive);

        var digests = record.Split('\n')
            .Where(static line => line.StartsWith("sha256 ", StringComparison.Ordinal))
            .Select(static line => line["sha256 ".Length..].Split(": ", 2, StringSplitOptions.None))
            .ToList();
        digests.Select(static pair => pair[0]).ShouldBe(["xterm.mjs", "xterm.css", "LICENSE", "addon-fit.mjs", "LICENSE.addon-fit"], ignoreOrder: true);
        foreach (var pair in digests)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(XtermDirectory, pair[0]))));
            actual.ShouldBe(pair[1].Trim(), pair[0] + " differs from the digest recorded in VERSION.txt");
        }
    }

    /// <summary>The terminal module imports xterm only from the vendored folder by relative path, and no shipped script or stylesheet of the screens loads anything from another origin.</summary>
    [Fact]
    public void TerminalModuleLoadsXtermFromTheAppOnly()
    {
        var module = File.ReadAllLines(Path.Combine(ScriptsDirectory, "autonogrammer-terminal.js"));

        var imports = module.Where(static line => line.StartsWith("import ", StringComparison.Ordinal)).ToList();
        imports.Count.ShouldBe(2);
        imports.ShouldAllBe(line => line.Contains("from \"../lib/xterm/", StringComparison.Ordinal));
        foreach (var file in new[] { "autonogrammer-terminal.js", "autonogrammer-kanban.js", "autonogrammer-dialog.js" })
        {
            var text = File.ReadAllText(Path.Combine(ScriptsDirectory, file));
            text.ShouldNotContain("http://", Case.Sensitive, file);
            text.ShouldNotContain("https://", Case.Sensitive, file);
        }

        foreach (var style in new[] { "kanban.css", "settings.css", "terminal.css" })
        {
            var css = CssSources.StripComments(CssSources.ReadStyle(style));
            css.ShouldNotContain("http://", Case.Sensitive, style);
            css.ShouldNotContain("https://", Case.Sensitive, style);
        }
    }

    /// <summary>The terminal screen reads its colours, face and size from the same tokens as the stylesheets, with xterm's screen-reader mode on and the screen read-only.</summary>
    [Fact]
    public void TerminalScreenTakesItsThemeFromTheTokensAndTurnsScreenReaderModeOn()
    {
        var module = File.ReadAllText(Path.Combine(ScriptsDirectory, "autonogrammer-terminal.js"));

        foreach (var token in new[] { "--autonogrammer-surface-sunken", "--autonogrammer-text", "--autonogrammer-accent", "--autonogrammer-font-mono", "--autonogrammer-font-size-terminal", "--autonogrammer-line-height-mono" })
        {
            module.ShouldContain(token, Case.Sensitive);
        }

        module.ShouldContain("screenReaderMode: true", Case.Sensitive);
        module.ShouldContain("disableStdin: true", Case.Sensitive);
        ContainsHexColour(module).ShouldBeFalse("the terminal palette must come from tokens, not literals");
        var entryCss = CssSources.StripComments(CssSources.ReadStyle("autonogrammer.css"));
        entryCss.IndexOf("@import url('../lib/xterm/xterm.css');", StringComparison.Ordinal).ShouldBeGreaterThan(-1);
        entryCss.IndexOf("@import url('../lib/xterm/xterm.css');", StringComparison.Ordinal).ShouldBeLessThan(entryCss.IndexOf("@import url('terminal.css');", StringComparison.Ordinal), "xterm's own rules come before the terminal's");
        File.Exists(Path.Combine(XtermDirectory, "xterm.css")).ShouldBeTrue();
    }

    /// <summary>All ten Kanban columns keep their full width and the board scrolls sideways, so no column is cut off (the defect of the desktop evidence).</summary>
    [Fact]
    public void KanbanColumnsKeepTheirWidthAndTheBoardScrolls()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("kanban.css"));

        RuleBody(css, ".autonogrammer-kanban-column").ShouldContain("flex: 0 0 var(--autonogrammer-kanban-column);", Case.Sensitive);
        RuleBody(css, ".autonogrammer-kanban-column").ShouldContain("min-width: 240px;", Case.Sensitive);
        RuleBody(css, ".autonogrammer-kanban-columns").ShouldContain("overflow-x: auto;", Case.Sensitive);
    }

    /// <summary>Below 840 px the Settings dialog fills the screen with no radius; above it the dialog is centred and the navigation keeps the settings-nav width.</summary>
    [Fact]
    public void SettingsDialogIsFullScreenBelowTheBreakpointAndTwoPaneAbove()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("settings.css"));

        RuleBody(css, ".autonogrammer-settings-body").ShouldContain("grid-template-columns: var(--autonogrammer-settings-nav) minmax(0, 1fr);", Case.Sensitive);
        var narrow = css[css.IndexOf("@media (max-width: 839.98px) {", StringComparison.Ordinal)..];
        var fullScreen = RuleBody(narrow, ".autonogrammer-settings");
        fullScreen.ShouldContain("width: 100vw;", Case.Sensitive);
        fullScreen.ShouldContain("height: 100dvh;", Case.Sensitive);
        fullScreen.ShouldContain("border-radius: 0;", Case.Sensitive);
        RuleBody(css, ".autonogrammer-settings-page-content").ShouldContain("max-width: var(--autonogrammer-content-max);", Case.Sensitive);
    }

    /// <summary>The settings controls that are not shared ones keep the 32 px target height.</summary>
    [Fact]
    public void SettingsControlsKeepTheDesignedTargetHeight()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("settings.css"));

        RuleBody(css, ".autonogrammer-settings-toggle").ShouldContain("height: var(--autonogrammer-control-height);", Case.Sensitive);
        RuleBody(css, ".autonogrammer-settings-nav-item").ShouldContain("min-height: var(--autonogrammer-control-height);", Case.Sensitive);
        RuleBody(css, ".autonogrammer-settings-check").ShouldContain("min-height: var(--autonogrammer-control-height);", Case.Sensitive);
        RuleBody(CssSources.StripComments(CssSources.ReadStyle("kanban.css")), ".autonogrammer-kanban-card-open").ShouldContain("min-height: var(--autonogrammer-control-height);", Case.Sensitive);
        double.Parse(CssSources.EffectiveTokens(CssSources.Theme.Light)["--autonogrammer-control-height"].Replace("px", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture).ShouldBe(32);
    }

    private static string RuleBody(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, selector + " has no rule");
        var open = css.IndexOf('{', start);
        var close = css.IndexOf('}', open);
        return css[(open + 1)..close];
    }

    private static bool ContainsHexColour(string text)
    {
        for (var index = 0; index < text.Length - 4; index++)
        {
            if (text[index] != '#')
            {
                continue;
            }

            var digits = 0;
            while (index + 1 + digits < text.Length && Uri.IsHexDigit(text[index + 1 + digits]))
            {
                digits++;
            }

            if (digits is 3 or 4 or 6 or 8)
            {
                return true;
            }
        }

        return false;
    }
}
