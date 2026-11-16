using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>Checks the presence of the documented ADR and audit references.</summary>
public sealed partial class CoworkAuditTests
{
    private static readonly string[] AdrRows = new[]
    {
        "ADR-0030", "ADR-0031", "ADR-0032", "ADR-0033", "ADR-0034",
        "ADR-0035", "ADR-0036", "ADR-0037"
    };

    [Fact]
    public void Adr0030To0037_AndThreeAuditDocs_Present()
    {
        var root = RepoRoot;
        // ADRs listed in decisions/README.md or SUMMARY (per amendment assert presence)
        var decisions = File.ReadAllText(Path.Combine(root, "docs", "decisions", "README.md"));
        foreach (var adr in AdrRows)
        {
            decisions.ShouldContain(adr);
        }
        // three audit docs exist (from 77-02/03/04)
        File.Exists(Path.Combine(root, "docs/quality/v0.16.0-security-review.md")).ShouldBeTrue();
        File.Exists(Path.Combine(root, "docs/quality/v0.16.0-accessibility-pass.md")).ShouldBeTrue();
        File.Exists(Path.Combine(root, "docs/parity/cowork-parity.md")).ShouldBeTrue();
        // also the reference and isolation promised by D-08
        File.Exists(Path.Combine(root, "docs/reference/cowork-extensions.md")).ShouldBeTrue();
        File.Exists(Path.Combine(root, "docs/runbooks/cowork-isolation.md")).ShouldBeTrue();
    }
}
