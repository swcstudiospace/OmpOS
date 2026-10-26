using System.Globalization;
using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Structure gates for the decision records in docs/decisions. A record that adopts a component must name the
/// candidate, give its licence as an SPDX identifier with a source URL, say how the choice was or will be verified,
/// and list the open risks. These checks read the real files: they prove the sections exist and carry the required
/// facts, not that the facts are true. Records numbered from <see cref="FirstEnforcedNumber"/> are checked; records
/// numbered from <see cref="FirstStrictNumber"/> must use the exact headings of the template.
/// </summary>
public sealed class DecisionRecordTests
{
    private const int FirstEnforcedNumber = 7;

    private const int FirstStrictNumber = 13;

    // Records that fix an internal policy or composition and choose no third-party candidate, so there is no licence
    // to name. Each entry carries the reason. An entry must name an existing record.
    private static readonly Dictionary<int, string> Grandfathered = new Dictionary<int, string>
    {
        [8] = "contract versioning policy: an internal rule, no third-party component chosen",
        [11] = "logging split by layer: an internal rule over components already adopted by earlier records",
        [12] = "agent loop plugins: composition of sibling projects whose licences are recorded in docs/plugins.md",
    };

    private static readonly string[] SpdxIdentifiers =
    [
        "MIT",
        "Apache-2.0",
        "BSD-2-Clause",
        "BSD-3-Clause",
        "ISC",
        "MPL-2.0",
        "AGPL-3.0-or-later",
        "GPL-3.0-or-later",
        "LGPL-3.0-or-later",
    ];

    // Headings of the template. Records from FirstStrictNumber must have each as a level-two heading with content.
    private static readonly string[] StrictHeadings = ["Candidate chosen", "Licence", "Verification route", "Open risks"];

    // Heading words accepted for the same facts in the older records.
    private static readonly string[] CandidateHeadingWords = ["Candidate", "Decision", "Verdict"];

    private static readonly string[] VerificationHeadingWords = ["Verification", "Evidence", "Runs", "Method", "Spike"];

    private static readonly string[] RiskHeadingWords = ["Risk", "Consequences", "Not measured", "Blockers", "Open"];

    private static readonly RequiredTopic[] RequiredTopics =
    [
        new("editor", ["code editor"]),
        new("terminal", ["terminal control"]),
        new("MCP client", ["MCP client"]),
        new("MCP gateway", ["MCP gateway"]),
        new("browser pane", ["browser pane"]),
        new("plugin isolation", ["plugin isolation"]),
    ];

    [Fact]
    public void EveryDecisionRecordNamesCandidateLicenceSourceVerificationAndRisks()
    {
        var records = LoadRecords(ProjectGraphChecker.FindRepoRoot());
        var numbers = records.Select(static adr => adr.Number).ToList();

        foreach (var (number, reason) in Grandfathered)
        {
            numbers.ShouldContain(number, $"the grandfathered list names {IdOf(number)}, which does not exist");
            reason.ShouldNotBeNullOrWhiteSpace($"the grandfathered entry {IdOf(number)} needs a reason");
        }

        var enforced = records
            .Where(static adr => adr.Number >= FirstEnforcedNumber && !Grandfathered.ContainsKey(adr.Number))
            .ToList();

        // A floor, so a rename of the files or a change of the numbering cannot make the loop below pass on nothing.
        enforced.Count.ShouldBeGreaterThanOrEqualTo(5, "ADR-0007, 0009, 0010, 0013 and 0014 are checked by this rule");

        var problems = new List<string>();
        foreach (var adr in enforced)
        {
            if (!adr.Lines.Any(static line => line.StartsWith("- Status:", StringComparison.Ordinal)))
            {
                problems.Add($"{adr.FileName}: no '- Status:' line");
            }

            if (adr.Number >= FirstStrictNumber)
            {
                CheckStrict(adr, problems);
            }
            else
            {
                CheckLegacy(adr, problems);
            }
        }

        problems.ShouldBeEmpty("decision records must name the candidate, licence (SPDX and source URL), verification and risks:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void ReadmeIndexListsEveryAdr()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var records = LoadRecords(repoRoot);
        var readme = File.ReadAllLines(Path.Combine(repoRoot, "docs", "decisions", "README.md"));

        var rows = readme.Where(static line => line.StartsWith("| [ADR-", StringComparison.Ordinal)).ToList();
        rows.ShouldNotBeEmpty("docs/decisions/README.md must index the records in a table");

        var problems = new List<string>();
        var indexed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            CheckIndexRow(repoRoot, row, indexed, problems);
        }

        foreach (var adr in records)
        {
            var count = indexed.GetValueOrDefault(adr.FileName);
            if (count != 1)
            {
                problems.Add($"{adr.FileName} appears {count.ToString(CultureInfo.InvariantCulture)} times in the README index; it must appear once");
            }
        }

        records.Select(static adr => adr.Number).Distinct().Count().ShouldBe(records.Count, "ADR numbers must be unique");
        problems.ShouldBeEmpty("docs/decisions/README.md must list every ADR exactly once:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void RequiredDecisionTopicsHaveARecord()
    {
        var records = LoadRecords(ProjectGraphChecker.FindRepoRoot());

        var missing = RequiredTopics
            .Where(topic => !records.Any(adr => topic.TitleWords.Any(word => adr.Title.Contains(word, StringComparison.OrdinalIgnoreCase))))
            .Select(static topic => topic.Name)
            .ToList();

        missing.ShouldBeEmpty("every required topic needs a decision record whose title names it; missing: " + string.Join(", ", missing));
    }

    private static void CheckIndexRow(string repoRoot, string row, Dictionary<string, int> indexed, List<string> problems)
    {
        var cells = row.Trim().Trim('|').Split('|');
        if (cells.Length < 4)
        {
            problems.Add($"index row needs four columns (ADR, Title, Status, Amendments): {row}");
            return;
        }

        var label = cells[0].Trim();
        var close = label.IndexOf("](", StringComparison.Ordinal);
        if (!label.StartsWith('[') || close < 0 || !label.EndsWith(')'))
        {
            problems.Add($"index row has no [ADR-NNNN](file.md) link: {row}");
            return;
        }

        var id = label[1..close];
        var target = label[(close + 2)..^1];
        if (!File.Exists(Path.Combine(repoRoot, "docs", "decisions", target)))
        {
            problems.Add($"index row {id} links {target}, which does not exist");
        }
        else if (!string.Equals(IdOf(ParseNumber(target)), id, StringComparison.Ordinal))
        {
            problems.Add($"index row {id} links {target}, which has another number");
        }

        if (string.IsNullOrWhiteSpace(cells[1]) || string.IsNullOrWhiteSpace(cells[2]))
        {
            problems.Add($"index row {id} needs a title and a status");
        }

        indexed[target] = indexed.GetValueOrDefault(target) + 1;
    }

    private static void CheckStrict(DecisionRecord adr, List<string> problems)
    {
        foreach (var heading in StrictHeadings)
        {
            if (SectionLines(adr, heading).All(string.IsNullOrWhiteSpace))
            {
                problems.Add($"{adr.FileName}: missing or empty section '## {heading}'");
            }
        }

        var licence = string.Join('\n', SectionLines(adr, "Licence"));
        if (!HasSpdxIdentifier(licence))
        {
            problems.Add($"{adr.FileName}: '## Licence' names no SPDX identifier");
        }

        if (!licence.Contains("https://", StringComparison.Ordinal))
        {
            problems.Add($"{adr.FileName}: '## Licence' gives no https:// source URL");
        }

        var verification = string.Join('\n', SectionLines(adr, "Verification route"));
        if (!verification.Contains("dotnet ", StringComparison.Ordinal)
            && !verification.Contains("docs/", StringComparison.Ordinal)
            && !verification.Contains("tests/", StringComparison.Ordinal))
        {
            problems.Add($"{adr.FileName}: '## Verification route' names no command or path");
        }

        if (!SectionLines(adr, "Open risks").Any(IsListItem))
        {
            problems.Add($"{adr.FileName}: '## Open risks' lists no risk");
        }
    }

    private static void CheckLegacy(DecisionRecord adr, List<string> problems)
    {
        var headings = adr.Lines
            .Where(static line => line.StartsWith('#'))
            .Select(static line => line.TrimStart('#').Trim())
            .ToList();

        if (!HasHeadingWord(headings, CandidateHeadingWords))
        {
            problems.Add($"{adr.FileName}: no heading names the candidate (words: {string.Join(", ", CandidateHeadingWords)})");
        }

        var text = string.Join('\n', adr.Lines);
        if (!HasSpdxIdentifier(text))
        {
            problems.Add($"{adr.FileName}: names no SPDX licence identifier");
        }

        if (!text.Contains("https://", StringComparison.Ordinal))
        {
            problems.Add($"{adr.FileName}: gives no https:// source URL");
        }

        if (!HasHeadingWord(headings, VerificationHeadingWords))
        {
            problems.Add($"{adr.FileName}: no heading names the verification (words: {string.Join(", ", VerificationHeadingWords)})");
        }

        if (!HasHeadingWord(headings, RiskHeadingWords))
        {
            problems.Add($"{adr.FileName}: no heading names the risks (words: {string.Join(", ", RiskHeadingWords)})");
        }
    }

    private static bool HasHeadingWord(List<string> headings, string[] words)
    {
        return headings.Any(heading => words.Any(word => heading.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasSpdxIdentifier(string text)
    {
        return SpdxIdentifiers.Any(identifier => ContainsToken(text, identifier));
    }

    // True when the identifier stands alone: no letter, digit or hyphen directly before or after it, so "MIT" does not
    // match inside "LIMITED" and "Apache-2.0" does not match inside "Apache-2.0-ext".
    private static bool ContainsToken(string text, string token)
    {
        for (var index = text.IndexOf(token, StringComparison.Ordinal); index >= 0; index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
        {
            var before = index == 0 ? ' ' : text[index - 1];
            var afterIndex = index + token.Length;
            var after = afterIndex >= text.Length ? ' ' : text[afterIndex];
            if (!IsTokenCharacter(before) && !IsTokenCharacter(after))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTokenCharacter(char c)
    {
        return char.IsAsciiLetterOrDigit(c) || c == '-';
    }

    private static bool IsListItem(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- ", StringComparison.Ordinal)
            || (trimmed.Length > 2 && char.IsAsciiDigit(trimmed[0]) && trimmed[1] == '.');
    }

    // The lines under "## heading" up to the next level-two heading; empty when the heading is absent.
    private static List<string> SectionLines(DecisionRecord adr, string heading)
    {
        var body = new List<string>();
        var inside = false;
        foreach (var line in adr.Lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                inside = string.Equals(line[3..].Trim(), heading, StringComparison.Ordinal);
                continue;
            }

            if (inside)
            {
                body.Add(line);
            }
        }

        return body;
    }

    private static List<DecisionRecord> LoadRecords(string repoRoot)
    {
        var directory = Path.Combine(repoRoot, "docs", "decisions");
        var records = new List<DecisionRecord>();
        foreach (var path in Directory.EnumerateFiles(directory, "ADR-*.md").Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            var number = ParseNumber(fileName);
            number.ShouldBeGreaterThan(0, $"{fileName} must be named ADR-NNNN-slug.md");

            var lines = File.ReadAllLines(path);
            lines.Length.ShouldBeGreaterThan(0, $"{fileName} must not be empty");
            records.Add(new DecisionRecord(number, fileName, TitleOf(lines[0]), lines));
        }

        return records;
    }

    // "ADR-0007-slug.md": the prefix, four digits, a hyphen, then the slug. Returns -1 for any other name.
    private static int ParseNumber(string fileName)
    {
        if (fileName.Length < 10 || !fileName.StartsWith("ADR-", StringComparison.Ordinal) || fileName[8] != '-')
        {
            return -1;
        }

        return int.TryParse(fileName.AsSpan(4, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
    }

    private static string IdOf(int number)
    {
        return "ADR-" + number.ToString("D4", CultureInfo.InvariantCulture);
    }

    // "# ADR-0007: Code editor ..." gives "Code editor ...".
    private static string TitleOf(string firstLine)
    {
        var colon = firstLine.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? firstLine.TrimStart('#', ' ') : firstLine[(colon + 1)..].Trim();
    }

    /// <summary>One decision record: its number, file name, title (the first line after the colon) and every line.</summary>
    private sealed record DecisionRecord(int Number, string FileName, string Title, string[] Lines);

    /// <summary>A topic that needs a record whose title contains one of the words, ignoring case.</summary>
    private sealed record RequiredTopic(string Name, string[] TitleWords);
}
