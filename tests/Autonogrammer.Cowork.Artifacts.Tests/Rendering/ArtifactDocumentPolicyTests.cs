using Autonogrammer.Cowork.Artifacts.Rendering;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Rendering;

/// <summary>Exact CSP strings, response headers, and the Mermaid host script.</summary>
public sealed class ArtifactDocumentPolicyTests
{
    [Fact]
    public void HtmlPolicyMatchesTheAdrText()
    {
        ArtifactDocumentPolicy.ForHtml(scripts: true).ShouldBe(ArtifactDocumentPolicy.HtmlScriptsOn);
        ArtifactDocumentPolicy.HtmlScriptsOn.ShouldBe(
            "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; manifest-src 'none'");
        ArtifactDocumentPolicy.ForHtml(scripts: false).ShouldContain("script-src 'none'");
        ArtifactDocumentPolicy.ForHtml(scripts: false).ShouldNotContain("unsafe-inline'; style");
        ArtifactDocumentPolicy.ForSvgOrImage().ShouldBe("default-src 'none'; img-src data:");
        ArtifactDocumentPolicy.HtmlScriptsOn.ShouldNotContain("unsafe-eval");
    }

    [Fact]
    public void ApprovedOriginsNeverReachScriptSrc()
    {
        var policy = ArtifactDocumentPolicy.ForHtml(true, ["https://api.example"]);
        var script = policy.Split(';', StringSplitOptions.TrimEntries).Single(part => part.StartsWith("script-src", StringComparison.Ordinal));
        script.ShouldBe("script-src 'unsafe-inline'");
        script.ShouldNotContain("api.example");
        policy.ShouldContain("connect-src https://api.example");
        policy.ShouldContain("img-src data: blob: https://api.example");
        policy.ShouldContain("font-src data: https://api.example");
        policy.ShouldContain("media-src data: blob: https://api.example");
    }

    [Fact]
    public void ResponseHeadersCarrySandboxAndFrameAncestors()
    {
        var headers = ArtifactDocumentPolicy.RenderResponseHeaders(["https://web.example"]);
        var csp = headers[0];
        csp.Name.ShouldBe("Content-Security-Policy");
        csp.Value.ShouldStartWith(ArtifactDocumentPolicy.HtmlScriptsOn);
        csp.Value.ShouldContain("sandbox allow-scripts");
        csp.Value.ShouldContain("frame-ancestors https://web.example");
        csp.Value.ShouldNotContain("script-src https://web.example");
        headers.ShouldContain(header => header.Name == "Referrer-Policy" && header.Value == "no-referrer");
        headers.ShouldContain(header => header.Name == "X-Content-Type-Options" && header.Value == "nosniff");
        headers.ShouldContain(header => header.Name == "Cache-Control" && header.Value == "no-store");
        headers.ShouldContain(header => header.Name == "Permissions-Policy" && header.Value == ArtifactDocumentPolicy.PermissionsPolicy);
        headers.ShouldNotContain(header => header.Name == "X-Frame-Options");

        var empty = ArtifactDocumentPolicy.RenderResponseHeaders([]);
        empty[0].Value.ShouldContain("frame-ancestors 'none'");
    }

    [Fact]
    public void MermaidUsesStrictKeysAndFallsBackWhenTheAssetIsMissing()
    {
        var missing = MermaidDocumentBuilder.Build("graph TD", new MissingAssets());
        missing.Available.ShouldBeFalse();
        missing.Reason.ShouldBe("MermaidAssetMissing");
        missing.Source.ShouldBe("graph TD");
        missing.Html.ShouldBeNull();

        var built = MermaidDocumentBuilder.Build("A-->B <script>", new FakeAssets());
        built.Available.ShouldBeTrue();
        built.Html.ShouldNotBeNull();
        built.Html.ShouldContain("securityLevel:'strict'");
        built.Html.ShouldContain("startOnLoad:false");
        built.Html.ShouldContain("maxTextSize:50000");
        built.Html.ShouldContain("maxEdges:500");
        built.Html.ShouldContain("htmlLabels:false");
        built.Html.ShouldContain("&lt;script&gt;");
        built.Html.ShouldNotContain("<script>A");
    }

    private sealed class MissingAssets : IMermaidAssetProvider
    {
        public string? GetScript() => null;
    }

    private sealed class FakeAssets : IMermaidAssetProvider
    {
        public string? GetScript() => "/* mermaid */";
    }
}
