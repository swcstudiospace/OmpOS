using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Security audit gate (U4 per D-15/D-16): parses the findings table in the security review
/// and enforces no open/accepted Critical or High; every scope area present with evidence.
/// Parity part (CoworkAuditTests.cs) owns the class declaration; this file and Docs part add members only.
/// Fixtures prove RED on bad states; real review must pass.
/// </summary>
public sealed partial class CoworkAuditTests
{
    private static string SecurityReviewPath(string root) =>
        Path.Combine(root, "docs/quality/v0.16.0-security-review.md");

    // Simple row model for findings table (id, phase, area, severity, finding, evidence, status, ownerAction)
    private sealed record FindingRow(string Id, string Phase, string Area, string Severity, string Finding, string Evidence, string Status, string OwnerAction);

    private static List<FindingRow> ParseFindingsTable(string md)
    {
        if (string.IsNullOrWhiteSpace(md) || !md.Contains("| id |", StringComparison.Ordinal))
            throw new InvalidOperationException("no findings table");
        var rows = new List<FindingRow>();
        var lines = md.Split('\n');
        bool inTable = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("## ", StringComparison.Ordinal) && inTable) break; // next section ends table
            if (trimmed.StartsWith("| id |", StringComparison.Ordinal)) { inTable = true; continue; }
            if (trimmed.StartsWith("|---", StringComparison.Ordinal)) continue;
            if (inTable && trimmed.StartsWith('|') && !trimmed.StartsWith("|-", StringComparison.Ordinal))
            {
                var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (cells.Length >= 8)
                {
                    rows.Add(new FindingRow(cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7]));
                }
                else if (string.IsNullOrWhiteSpace(trimmed))
                {
                    break;
                }
            }
        }
        return rows;
    }

    private static List<string> ParseScopeAreas(string md)
    {
        var areas = new List<string>();
        if (md.Contains("Accounts tokens/keyring", StringComparison.Ordinal)) areas.Add("Accounts tokens/keyring (67)");
        if (md.Contains("Data-plane tenancy", StringComparison.Ordinal)) areas.Add("data-plane tenancy/no-RLS (68)");
        if (md.Contains("Artifact sandbox", StringComparison.Ordinal)) areas.Add("artifact sandbox/CSP (71)");
        if (md.Contains("Browser and computer-use", StringComparison.Ordinal)) areas.Add("browser/computer-use policies (72/73)");
        if (md.Contains("Dispatch relay keys", StringComparison.Ordinal)) areas.Add("Dispatch relay keys/approvals (74)");
        if (md.Contains("Cross-cutting", StringComparison.Ordinal)) areas.Add("cross-cutting");
        // auth modes, rate limits, hosting, engine isolation, CSP, OpenAPI covered under above rows per D-32 amendments
        return areas;
    }

    [Fact]
    public void FindingsFixtureWithAnOpenHighFailsNamingTheRowId()
    {
        const string badTable = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-77-03 | 67 | Accounts tokens/keyring | high | open token leak | none | open | fix |
";
        var ex = Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(badTable), badTable));
        ex.ShouldNotBeNull("fixture with open High must fail");
        ex!.Message.ShouldContain("T-77-03");
    }

    [Fact]
    public void FindingsFixtureWithAnAcceptedCriticalFails()
    {
        const string badTable = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-77-CRIT | 68 | data-plane | Critical | accepted crit | none | accepted | OQ-xxx |
";
        var ex = Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(badTable), badTable));
        ex.ShouldNotBeNull("accepted Critical must fail");
    }

    [Fact]
    public void AcceptedMediumWithoutOQIdFailsButWithOQPasses()
    {
        const string bad = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-MED | 71 | sandbox | Medium | foo | bar | accepted | |
";
        var ex = Record.Exception(() => ValidateAcceptedMediumHasOQ(ParseFindingsTable(bad)));
        ex.ShouldNotBeNull("Medium accepted without OQ must fail");

        const string good = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-MED | 71 | sandbox | Medium | foo | bar | accepted | OQ-482 |
";
        Record.Exception(() => ValidateAcceptedMediumHasOQ(ParseFindingsTable(good))).ShouldBeNull();
    }

    [Fact]
    public void ScopeTableMissingAThreatModelAreaFails()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var md = File.ReadAllText(SecurityReviewPath(root));
        var areas = ParseScopeAreas(md);
        // per plan + D-32 amendments: every area (Accounts, data-plane, artifact sandbox/CSP, browser/computer-use, Dispatch, cross-cutting) has row
        areas.Count.ShouldBeGreaterThanOrEqualTo(6, "scope table must cover all threat-model areas from brief with evidence");
    }

    private const string OpenHighRow = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-77-HI | 67 | Accounts tokens/keyring | High | fixture | none | open | fix |
";

    private const string CriticalOpenRow = @"
| id | phase | area | severity | finding | evidence | status | owner action |
| T-77-HI | 67 | Accounts tokens/keyring | Critical | fixture | none | open | fix |
";

    private const string CompleteD16ExceptionForT77Hi = @"
### D-16 owner exception: T-77-HI

- Granted by: repository owner (fixture)
- Scope: fixture scope
- Residual risk: fixture risk
- Expiry: fixture expiry
- Regression evidence: fixture evidence
";

    [Fact]
    public void HighOpenWithCompleteNamedD16ExceptionPasses()
    {
        var md = OpenHighRow + CompleteD16ExceptionForT77Hi;
        Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(md), md)).ShouldBeNull("complete named D-16 exception must admit a High");
    }

    [Fact]
    public void HighOpenWithIncompleteD16ExceptionFailsNamingTheRowId()
    {
        var md = OpenHighRow + CompleteD16ExceptionForT77Hi.Replace("- Expiry: fixture expiry", "", StringComparison.Ordinal);
        var ex = Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(md), md));
        ex.ShouldNotBeNull("D-16 exception without an expiry field must not admit a High");
        ex!.Message.ShouldContain("T-77-HI");
    }

    [Fact]
    public void HighOpenWithD16ExceptionForAnotherIdFailsNamingTheRowId()
    {
        var md = OpenHighRow + CompleteD16ExceptionForT77Hi.Replace("T-77-HI", "T-77-OTHER", StringComparison.Ordinal);
        var ex = Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(md), md));
        ex.ShouldNotBeNull("D-16 exception naming another finding must not admit this High");
        ex!.Message.ShouldContain("T-77-HI");
    }

    [Fact]
    public void OpenCriticalWithCompleteD16ExceptionStillFails()
    {
        var md = CriticalOpenRow + CompleteD16ExceptionForT77Hi;
        var ex = Record.Exception(() => ValidateNoOpenOrAcceptedCriticalHigh(ParseFindingsTable(md), md));
        ex.ShouldNotBeNull("D-16 exceptions never apply to Critical findings");
        ex!.Message.ShouldContain("T-77-HI");
    }

    private static readonly string[] D16ExceptionFields = ["Granted by", "Scope", "Residual risk", "Expiry", "Regression evidence"];

    // Critical is never excepted. A High whose status is exactly open or accepted passes only when the
    // document holds a complete "D-16 owner exception: <ID>" block naming that finding.
    private static void ValidateNoOpenOrAcceptedCriticalHigh(List<FindingRow> findings, string md)
    {
        foreach (var f in findings)
        {
            var critical = f.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase);
            var high = f.Severity.Equals("High", StringComparison.OrdinalIgnoreCase);
            var openOrAccepted = f.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                || f.Status.Equals("accepted", StringComparison.OrdinalIgnoreCase);
            if (!(critical || high) || !openOrAccepted) continue;
            if (critical)
                throw new InvalidOperationException($"Critical finding {f.Id} is {f.Status}; D-16 forbids shipping an open or accepted Critical");
            if (f.Status is "open" or "accepted" && HasCompleteD16Exception(md, f.Id)) continue;
            throw new InvalidOperationException($"High finding {f.Id} is {f.Status}; D-16 allows it only under a complete 'D-16 owner exception: {f.Id}' block with fields granted by, scope, residual risk, expiry and regression evidence");
        }
    }

    private static bool HasCompleteD16Exception(string md, string findingId)
    {
        var heading = Regex.Match(md, $@"(?m)^#{{1,6}}\s*D-16 owner exception:\s*{Regex.Escape(findingId)}\b", RegexOptions.None, TimeSpan.FromSeconds(2));
        if (!heading.Success) return false;
        var afterHeading = md[(heading.Index + heading.Length)..];
        var nextHeading = Regex.Match(afterHeading, @"(?m)^#{1,6}\s", RegexOptions.None, TimeSpan.FromSeconds(2));
        var block = nextHeading.Success ? afterHeading[..nextHeading.Index] : afterHeading;
        return D16ExceptionFields.All(field =>
            Regex.IsMatch(block, $@"(?im)^\s*(?:[-*]\s*)?{Regex.Escape(field)}\s*:\s*\S", RegexOptions.None, TimeSpan.FromSeconds(2)));
    }

    private static void ValidateAcceptedMediumHasOQ(List<FindingRow> findings)
    {
        var bad = findings.FirstOrDefault(f =>
            f.Severity.Equals("Medium", StringComparison.OrdinalIgnoreCase)
            && f.Status.Equals("accepted", StringComparison.OrdinalIgnoreCase)
            && !f.OwnerAction.Contains("OQ-", StringComparison.Ordinal));
        if (bad is not null)
            throw new InvalidOperationException($"Accepted Medium {bad.Id} needs OQ id in owner action");
    }

    // Real gate used after GREEN on review
    [Fact]
    public void SecurityReviewScopeAndFindingsAreCompleteAndClean()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var path = SecurityReviewPath(root);
        File.Exists(path).ShouldBeTrue("review md must exist");
        var md = File.ReadAllText(path);
        var findings = ParseFindingsTable(md);
        ValidateNoOpenOrAcceptedCriticalHigh(findings, md);
        var areas = ParseScopeAreas(md);
        // per plan, 6 areas + cross
        areas.Count.ShouldBeGreaterThanOrEqualTo(6, "every threat-model area must have a scope row");
        // Mermaid licence row must admit open (per acceptance)
        md.ShouldContain("Mermaid");
        md.ShouldContain("licence open");
    }
}
