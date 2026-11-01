using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Export;
using Autonogrammer.Cowork.Artifacts.Model;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Export;

/// <summary>Deterministic zip and safe-default transforms.</summary>
public sealed class ArtifactExportTests
{
    [Fact]
    public void TwoExportsOfTheSameArtifactHashEqual()
    {
        var exporter = new ArtifactExporter(new ArtifactOptions());
        var versions = new[] { Snapshot(ArtifactKind.Markdown, "Notes", "# hi"u8.ToArray(), 1) };
        var first = exporter.ExportZip(versions, ArtifactExportChoice.Safe);
        var second = exporter.ExportZip(versions, ArtifactExportChoice.Safe);
        ArtifactExporter.Hash(first).ShouldBe(ArtifactExporter.Hash(second));
        first.Bytes.ShouldBe(second.Bytes);
        first.FileName.ShouldBe("Notes.zip");
    }

    [Fact]
    public void SafeSvgAndHtmlDefaultsAndOriginalRiskNote()
    {
        var exporter = new ArtifactExporter(new ArtifactOptions());
        var svg = Snapshot(ArtifactKind.Svg, "Diagram", Encoding.UTF8.GetBytes("<svg onload=\"x\"><script>alert(1)</script><g/></svg>"), 1);
        var safeSvg = ArtifactExporter.ExportFile(svg, ArtifactExportChoice.Safe);
        var text = Encoding.UTF8.GetString(safeSvg.Bytes.Span);
        text.ShouldNotContain("script");
        text.ShouldNotContain("onload");
        text.ShouldContain("<g/>");

        var html = Snapshot(ArtifactKind.Html, "Page", Encoding.UTF8.GetBytes("<!DOCTYPE html><html></html>"), 1);
        var safeHtml = ArtifactExporter.ExportFile(html, ArtifactExportChoice.Safe);
        var htmlText = Encoding.UTF8.GetString(safeHtml.Bytes.Span);
        htmlText.ShouldStartWith(ArtifactExporter.PanelMetaPolicy);
        htmlText.ShouldNotContain("DOCTYPE");

        var original = ArtifactExporter.ExportFile(html, ArtifactExportChoice.Original);
        original.RiskNote.ShouldBe(ArtifactExport.OriginalRiskNote);
        original.Bytes.Span.SequenceEqual(html.Content.Span).ShouldBeTrue();
    }

    private static ArtifactSnapshot Snapshot(ArtifactKind kind, string title, byte[] content, int number) => new()
    {
        ArtifactId = "018f1b2c-3d4e-7a8b-9c0d-112233445566",
        Kind = kind,
        Title = title,
        MediaType = "text/plain",
        Number = number,
        Content = content,
        ContentSha256 = new string('a', 64),
        Author = ArtifactAuthor.Agent,
        CreatedAt = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero),
    };
}
