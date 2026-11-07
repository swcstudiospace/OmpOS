namespace Autonogrammer.Harness.BrowserUse.Tests.Corpus;

/// <summary>The 19-page hostile corpus inventory. Host invariants only; model compliance is never claimed.</summary>
internal static class CorpusPages
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
