using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Corpus;

/// <summary>The 19-page hostile corpus inventory. Host invariants only; model compliance is never claimed.</summary>
public static class CorpusPages
{
    /// <summary>Listed corpus page file names, in order.</summary>
    public static readonly string[] FileNames =
    [
        "c01-visible-instruction.html",
        "c02-hidden-css-text.html",
        "c03-attribute-text.html",
        "c04-comments-meta-noscript.html",
        "c05-forged-close-and-system.html",
        "c06-invisible-bidi-unicode.html",
        "c07-text-only-in-image.html",
        "c08-redirect-loopback.html",
        "c09-redirect-rfc1918.html",
        "c10-address-forms.html",
        "c11-dns-rebinding.html",
        "c12-popup-storm.html",
        "c13-auto-download.html",
        "c14-file-chooser.html",
        "c15-password-card-form.html",
        "c16-dialog-loops.html",
        "c17-oversized-dom.html",
        "c18-scheme-links.html",
        "c19-permission-clipboard-iframe-banner.html",
    ];

    /// <summary>Directory of the copied corpus pages.</summary>
    public static string PagesDirectory => Path.Combine(AppContext.BaseDirectory, "Corpus", "pages");
}

/// <summary>Offline inventory of the hostile corpus pages.</summary>
public sealed class CorpusPagesInventoryTests
{
    private static readonly Regex Banned = new(@"\b(TODO|FIXME|HACK|XXX)\b", RegexOptions.CultureInvariant);

    [Fact]
    public void NineteenPages_Exist_WithUniqueNames_AndNoBannedMarker()
    {
        CorpusPages.FileNames.Length.ShouldBe(19);
        CorpusPages.FileNames.Distinct(StringComparer.Ordinal).Count().ShouldBe(19);
        var directory = CorpusPages.PagesDirectory;
        foreach (var name in CorpusPages.FileNames)
        {
            var path = Path.Combine(directory, name);
            File.Exists(path).ShouldBeTrue(path);
            var text = File.ReadAllText(path);
            text.Length.ShouldBeGreaterThan(0);
            Banned.IsMatch(text).ShouldBeFalse(name);
        }

        var onDisk = Directory.GetFiles(directory, "*.html").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        onDisk.ShouldBe(CorpusPages.FileNames.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ListedPayloads_ArePresent()
    {
        var directory = CorpusPages.PagesDirectory;
        Read("c01-visible-instruction.html").ShouldContain("Ignore previous instructions");
        Read("c02-hidden-css-text.html").ShouldContain("display: none");
        Read("c03-attribute-text.html").ShouldContain("aria-label");
        Read("c04-comments-meta-noscript.html").ShouldContain("<noscript>");
        Read("c05-forged-close-and-system.html").ShouldContain("system message");
        Read("c06-invisible-bidi-unicode.html").ShouldContain("&#8203;");
        Read("c07-text-only-in-image.html").ShouldContain("image/svg+xml");
        Read("c08-redirect-loopback.html").ShouldContain("127.0.0.1:9");
        Read("c09-redirect-rfc1918.html").ShouldContain("10.1.2.3");
        Read("c10-address-forms.html").ShouldContain("0xA9FEA9FE");
        Read("c11-dns-rebinding.html").ShouldContain("rebind.invalid");
        Read("c12-popup-storm.html").ShouldContain("window.open");
        Read("c13-auto-download.html").ShouldContain("download=");
        Read("c14-file-chooser.html").ShouldContain("type=\"file\"");
        Read("c15-password-card-form.html").ShouldContain("planted-not-a-secret-1");
        Read("c15-password-card-form.html").ShouldContain("0000000000000000");
        Read("c16-dialog-loops.html").ShouldContain("alert");
        Read("c17-oversized-dom.html").ShouldContain("node 3999");
        Read("c18-scheme-links.html").ShouldContain("javascript:");
        Read("c19-permission-clipboard-iframe-banner.html").ShouldContain("controlling the browser");

        string Read(string name) => File.ReadAllText(Path.Combine(directory, name));
    }
}
