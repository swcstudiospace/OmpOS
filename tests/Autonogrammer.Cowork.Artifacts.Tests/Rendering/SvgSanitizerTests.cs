using Autonogrammer.Cowork.Artifacts.Rendering;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Rendering;

/// <summary>Allowlist walk, CSS drop, and source fallback.</summary>
public sealed class SvgSanitizerTests
{
    [Fact]
    public void DoctypeAndEntityBombAreRejectedWhole()
    {
        var doctype = Sanitize("doctype.svg");
        doctype.Accepted.ShouldBeFalse();
        doctype.Document.ShouldBeNull();
        doctype.Findings.ShouldContain(finding => finding.Code == "doctype");
        doctype.Render().ShouldContain("doctype");
        doctype.Render().ShouldStartWith(doctype.Source);

        var bomb = Sanitize("entity-bomb.svg");
        bomb.Accepted.ShouldBeFalse();
        bomb.Source.Length.ShouldBeLessThan(500);
        bomb.Render().Length.ShouldBeLessThan(600);
        bomb.Render().ShouldContain("findings:");
    }

    [Fact]
    public void HostileElementsAndAttributesAreRemoved()
    {
        var foreign = Sanitize("foreign-object.svg");
        foreign.Accepted.ShouldBeTrue();
        var foreignDocument = foreign.Document.ShouldNotBeNull();
        foreignDocument.ShouldNotContain("foreignObject");
        foreign.Findings.ShouldContain(finding => finding.Code == "foreign_object");

        var script = Sanitize("script-element.svg");
        var scriptDocument = script.Document.ShouldNotBeNull();
        scriptDocument.ShouldNotContain("script");
        script.Findings.ShouldContain(finding => finding.Code == "script");

        var events = Sanitize("event-handler.svg");
        var eventDocument = events.Document.ShouldNotBeNull();
        eventDocument.ShouldNotContain("onload");
        eventDocument.ShouldNotContain("onclick");
        events.Findings.ShouldContain(finding => finding.Code == "event_handler");

        var external = Sanitize("external-ref.svg");
        var externalDocument = external.Document.ShouldNotBeNull();
        externalDocument.ShouldNotContain("https://evil.example");
        externalDocument.ShouldContain("#ok");
        external.Findings.ShouldContain(finding => finding.Code == "external_ref");
    }

    [Fact]
    public void CssImportIsDroppedAndPresentationAttributesSurvive()
    {
        var result = Sanitize("css-import.svg");
        var document = result.Document.ShouldNotBeNull();
        document.ShouldNotContain("@import");
        document.ShouldNotContain("url(");
        document.ShouldNotContain("<style");
        document.ShouldContain("fill=\"black\"");
        result.Findings.ShouldContain(finding => finding.Code == "css");
        result.Findings.ShouldContain(finding => finding.Code == "css");
        document.ShouldNotContain("string-edit");
    }

    private static SvgSanitizer.SvgSanitizeResult Sanitize(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "svg", name);
        return SvgSanitizer.Sanitize(File.ReadAllText(path));
    }
}
