using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The HTML shell loads the White and Purple stylesheet, the self-hosted fonts and the theme boot script, with no Bootstrap and no inline script (PX-08).</summary>
public sealed partial class IndexHtmlTests
{
    /// <summary>The shell links the shared stylesheet and the head's boot styles, and nothing from Bootstrap.</summary>
    [Fact]
    public void ShellLinksTheOmposStylesheetAndNoBootstrap()
    {
        var html = ReadIndex();

        html.ShouldContain("href=\"_content/OmpOS.UI/styles/ompos.css\"");
        html.ShouldContain("href=\"css/app.css\"");
        html.ShouldNotContain("bootstrap", Case.Insensitive);
        File.Exists(Path.Combine(UiWwwroot(), "styles", "ompos.css")).ShouldBeTrue();
        File.Exists(Path.Combine(WebWwwroot(), "css", "app.css")).ShouldBeTrue();
    }

    /// <summary>Every script tag loads a file (the CSP forbids inline script), and the theme boot script runs before the stylesheets so the first paint has its theme.</summary>
    [Fact]
    public void ScriptsAreFilesAndThemeBootRunsBeforeTheStylesheets()
    {
        var html = ReadIndex();

        foreach (Match script in ScriptTag().Matches(html))
        {
            var attributes = script.Groups["attributes"].Value;
            var body = script.Groups["body"].Value;
            if (attributes.Contains("type=\"importmap\"", StringComparison.Ordinal))
            {
                body.Trim().ShouldBeEmpty("the import map is filled in by the framework at publish time");
                continue;
            }

            attributes.ShouldContain("src=\"", Case.Sensitive, "an inline script is forbidden by script-src 'self'");
            body.Trim().ShouldBeEmpty();
        }

        var boot = html.IndexOf("_content/OmpOS.UI/scripts/theme-boot.js", StringComparison.Ordinal);
        var stylesheet = html.IndexOf("_content/OmpOS.UI/styles/ompos.css", StringComparison.Ordinal);
        boot.ShouldBeGreaterThan(0);
        boot.ShouldBeLessThan(stylesheet);
        File.Exists(Path.Combine(UiWwwroot(), "scripts", "theme-boot.js")).ShouldBeTrue();
        File.Exists(Path.Combine(UiWwwroot(), "scripts", "ompos-theme.js")).ShouldBeTrue();
    }

    /// <summary>The two body weights are preloaded from the static web asset path, and the files exist under the UI project's wwwroot.</summary>
    [Theory]
    [InlineData("DMSans/DMSans-Regular.woff2")]
    [InlineData("DMSans/DMSans-SemiBold.woff2")]
    public void BodyFontsArePreloadedWithCrossorigin(string font)
    {
        var html = ReadIndex();

        var preload = PreloadLink().Matches(html).Select(static match => match.Value).SingleOrDefault(link => link.Contains(font, StringComparison.Ordinal));
        preload.ShouldNotBeNull($"index.html must preload {font}");
        preload.ShouldContain("href=\"_content/OmpOS.UI/fonts/" + font + "\"", Case.Sensitive);
        preload.ShouldContain("as=\"font\"", Case.Sensitive);
        preload.ShouldContain("type=\"font/woff2\"", Case.Sensitive);
        preload.ShouldContain("crossorigin", Case.Sensitive);
        File.Exists(Path.Combine(UiWwwroot(), "fonts", font)).ShouldBeTrue();
    }

    /// <summary>The favicon, touch icon and manifest use the names the brand assets ship under, and the theme colour is declared.</summary>
    [Theory]
    [InlineData("href=\"favicon.ico\"")]
    [InlineData("href=\"favicon.svg\"")]
    [InlineData("href=\"favicon-32.png\"")]
    [InlineData("href=\"apple-touch-icon.png\"")]
    [InlineData("href=\"site.webmanifest\"")]
    [InlineData("<meta name=\"theme-color\"")]
    public void IconManifestAndThemeColourAreDeclared(string expected)
    {
        ReadIndex().ShouldContain(expected, Case.Sensitive);
    }

    /// <summary>The page title is the product name, not the template's project name.</summary>
    [Fact]
    public void TitleIsTheProductName()
    {
        ReadIndex().ShouldContain("<title>OmpOS</title>", Case.Sensitive);
    }

    private static string ReadIndex() => File.ReadAllText(Path.Combine(WebWwwroot(), "index.html"));

    private static string WebWwwroot() => Path.Combine(RepoRoot(), "apps", "OmpOS.Web", "wwwroot");

    private static string UiWwwroot() => Path.Combine(RepoRoot(), "src", "OmpOS.UI", "wwwroot");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return directory.FullName;
    }

    [GeneratedRegex("<script(?<attributes>[^>]*)>(?<body>.*?)</script>", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ScriptTag();

    [GeneratedRegex("<link[^>]*rel=\"preload\"[^>]*>", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PreloadLink();
}
