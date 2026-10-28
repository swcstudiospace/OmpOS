using System.Text.RegularExpressions;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>The shipped catalogue holds every one of the 92 Cursor tokens, each with a handler and an honest status, and each cites the Cursor research row it was verified against (VX-02, PX-12).</summary>
public sealed class SlashCommandCatalogueTests
{
    private const int DocumentedTokens = 92;

    /// <summary>
    /// Each shipped command names a research row, and that row exists in the research record that holds it, is a row of the command tables, and
    /// documents the command's name as its syntax. Rows A, B and C are in the first record; rows D are the tokens added after it. No row is cited for a command that is not shipped.
    /// </summary>
    [Fact]
    public void EveryCatalogueEntryCitesAResearchRow()
    {
        var root = FindRepositoryRoot();
        var research = File.ReadAllLines(Path.Combine(root, SlashCommandCatalogue.Reference.Source.Replace('/', Path.DirectorySeparatorChar)));
        var additions = File.ReadAllLines(Path.Combine(root, SlashCommandCatalogue.AdditionsReference.Source.Replace('/', Path.DirectorySeparatorChar)));
        var shippedNames = SlashCommandCatalogue.Shipped.ListAll().Select(command => command.Name).ToArray();

        shippedNames.ShouldNotBeEmpty();
        SlashCommandCatalogue.ResearchRows.Keys.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(shippedNames);
        foreach (var name in shippedNames)
        {
            var row = SlashCommandCatalogue.ResearchRows[name];
            Regex.IsMatch(row, "^[ABCD][0-9]{2}$", RegexOptions.None, TimeSpan.FromSeconds(1))
                .ShouldBeTrue($"Row '{row}' cited by /{name} is not a command row identifier.");

            if (row[0] == 'D')
            {
                var additionRow = $"| /{name} |";
                additions.Any(line => line.StartsWith(additionRow, StringComparison.Ordinal)).ShouldBeTrue($"Row {row} cited by /{name} is not in section 2a of the additions record.");
                continue;
            }

            var tableRow = research.SingleOrDefault(line => line.StartsWith($"| {row} |", StringComparison.Ordinal));
            tableRow.ShouldNotBeNull($"Row {row} cited by /{name} is not in the research record.");
            var syntax = tableRow.Split('|')[2];
            syntax.ShouldContain($"`/{name}", Case.Sensitive, $"Row {row} does not document the syntax of /{name}.");
        }
    }

    /// <summary>The catalogue holds exactly the 92 documented tokens (86 in the first research record and 6 added), with the documented split into groups.</summary>
    [Fact]
    public void TheCatalogueHoldsAllNinetyTwoTokens()
    {
        var all = SlashCommandCatalogue.Shipped.ListAll();

        all.Count.ShouldBe(DocumentedTokens);
        all.Select(command => command.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(DocumentedTokens);
        all.Count(command => command.Group == SlashCommandGroup.Cli).ShouldBe(65, "44 CLI table rows and 21 changelog rows");
        all.Count(command => command.Group == SlashCommandGroup.BuiltInSkill).ShouldBe(18);
        all.Count(command => command.Group == SlashCommandGroup.Chat).ShouldBe(9, "side, btw, in-cloud and the six tokens added since the first record");
        SlashCommandCatalogue.ResearchRows.Values.Count(row => row[0] == 'D').ShouldBe(6);
    }

    /// <summary>Every token has a handler: a native action, an omp command, a skill, a cloud hand-off or an unavailable marker with its reason. No unavailable row lacks a reason.</summary>
    [Fact]
    public void EveryTokenHasAHandlerAndEveryUnavailableRowHasAReason()
    {
        foreach (var command in SlashCommandCatalogue.Shipped.ListAll())
        {
            command.HandlerId.ShouldNotBeNullOrWhiteSpace($"/{command.Name} has no handler.");
            command.Description.ShouldNotBeNullOrWhiteSpace();
            if (command.Status == SlashCommandStatus.Unavailable)
            {
                command.DisabledReason.ShouldNotBeNullOrWhiteSpace($"/{command.Name} is unavailable and gives no reason.");
                command.Note.ShouldBeNull();
            }
            else
            {
                command.DisabledReason.ShouldBeNull($"/{command.Name} is enabled, so it must not carry a disabled reason.");
                command.Note.ShouldNotBeNullOrWhiteSpace($"/{command.Name} gives no note about how it runs.");
            }

            (command.Kind == SlashHandlerKind.Unavailable).ShouldBe(string.Equals(command.HandlerId, "unavailable", StringComparison.Ordinal), $"/{command.Name}: the unavailable kind and handler id disagree.");
        }
    }

    /// <summary>Each kind carries what it needs: a known action id, an omp command that omp 18.12.6 really handles over ACP, a skill name, or the cloud hand-off with the reason that cloud agents are phase 60.</summary>
    [Fact]
    public void EachHandlerKindIsBackedByWhatItNames()
    {
        var actions = new HashSet<string>(
            [SlashActionIds.SettingsOpen, SlashActionIds.ChatNew, SlashActionIds.TerminalNew, SlashActionIds.ModeSet, SlashActionIds.ModelPick, SlashActionIds.SideOpen, SlashActionIds.HelpShow],
            StringComparer.Ordinal);
        foreach (var command in SlashCommandCatalogue.Shipped.ListAll())
        {
            switch (command.Kind)
            {
                case SlashHandlerKind.NativeAction:
                    actions.ShouldContain(command.HandlerId, StringComparer.Ordinal, $"/{command.Name} names an action no host implements.");
                    command.Platforms.ShouldBe(CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS);
                    break;
                case SlashHandlerKind.OmpDelegated:
                    var omp = command.HandlerId["omp.".Length..];
                    command.HandlerId.ShouldStartWith("omp.", Case.Sensitive);
                    OmpAcpBuiltinCommands.AllNames.ShouldContain(omp, StringComparer.Ordinal, $"/{command.Name} is delegated to /{omp}, which omp {OmpAcpBuiltinCommands.OmpVersion} does not handle over ACP.");
                    OmpAcpBuiltinCommands.AllNames.ShouldContain(command.EngineCommand ?? command.Name, StringComparer.Ordinal, $"/{command.Name} is sent as /{command.EngineCommand ?? command.Name}, which omp does not handle over ACP.");
                    break;
                case SlashHandlerKind.Skill:
                    command.HandlerId.ShouldBe("skill." + command.Name);
                    command.Group.ShouldBe(SlashCommandGroup.BuiltInSkill);
                    command.Status.ShouldBe(SlashCommandStatus.Partial);
                    break;
                case SlashHandlerKind.CloudHandoff:
                    command.Status.ShouldBe(SlashCommandStatus.Unavailable);
                    command.DisabledReason.ShouldBe("cloud agents: phase 60");
                    break;
                default:
                    command.Status.ShouldBe(SlashCommandStatus.Unavailable);
                    break;
            }
        }
    }

    /// <summary>The delegated commands are exactly the ones omp handles; a Cursor token that omp only runs in its terminal UI is not delegated.</summary>
    [Theory]
    [InlineData("goal")]
    [InlineData("fork")]
    [InlineData("resume")]
    [InlineData("loop")]
    [InlineData("copy")]
    public void ATokenThatOmpRunsOnlyInItsTerminalUiIsNotDelegated(string name)
    {
        var command = SlashCommandCatalogue.Shipped.ListAll().Single(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        command.Kind.ShouldNotBe(SlashHandlerKind.OmpDelegated);
        OmpAcpBuiltinCommands.AllNames.ShouldNotContain(name, StringComparer.Ordinal);
    }

    /// <summary>The aliases name the token they are aliases of, and that token is itself in the catalogue.</summary>
    [Fact]
    public void EveryAliasNamesAnExistingToken()
    {
        var names = SlashCommandCatalogue.Shipped.ListAll().Select(command => command.Name).ToHashSet(StringComparer.Ordinal);
        var aliases = SlashCommandCatalogue.Shipped.ListAll().Where(command => command.AliasOf is not null).ToArray();

        aliases.Length.ShouldBe(13, "8 aliases in the CLI table and 5 in the changelog");
        aliases.ShouldAllBe(alias => names.Contains(alias.AliasOf!));
    }

    /// <summary>The published table in docs/reference/slash-commands.md and the catalogue agree on every token, group, handler kind, handler, status and reason.</summary>
    [Fact]
    public void TheDocumentedTableAndTheCatalogueAgree()
    {
        var rows = ReadDocumentedRows();
        var catalogue = SlashCommandCatalogue.Shipped.ListAll();

        rows.Select(row => row.Token).OrderBy(token => token, StringComparer.Ordinal).ShouldBe(catalogue.Select(command => command.Name).OrderBy(name => name, StringComparer.Ordinal));
        foreach (var command in catalogue)
        {
            var row = rows.Single(candidate => string.Equals(candidate.Token, command.Name, StringComparison.Ordinal));
            row.Group.ShouldBe(GroupLabel(command.Group), $"/{command.Name} group");
            row.Cursor.ShouldBe(command.Description, $"/{command.Name} description");
            row.Kind.ShouldBe(command.Kind.ToString(), $"/{command.Name} kind");
            row.Handler.ShouldBe(ExpectedHandler(command), $"/{command.Name} handler");
            row.Status.ShouldBe(command.Status.ToString(), $"/{command.Name} status");
            row.Reason.ShouldBe(command.DisabledReason ?? command.Note, $"/{command.Name} reason");
        }
    }

    /// <summary>The open-ended forms are documented next to the table.</summary>
    [Fact]
    public void TheOpenEndedFormsAreDocumented()
    {
        var text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "reference", "slash-commands.md"));

        text.ShouldContain("`/<skill-name>`", Case.Sensitive);
        text.ShouldContain("`/<command-name> [arguments]`", Case.Sensitive);
        text.ShouldContain("`/<subagent-name> <task>`", Case.Sensitive);
        text.ShouldContain("`& <task>`", Case.Sensitive);
    }

    /// <summary>The palette resolves every one of the 92 tokens: each is listed, and each unavailable token is listed disabled with its reason and the Cursor badge.</summary>
    [Fact]
    public void EveryTokenIsListedInThePaletteWithItsStatus()
    {
        var palette = new SlashPaletteModel(SlashCommandCatalogue.Shipped);
        palette.SetActionHost(new AllowAllHost());

        palette.UpdateDraft("/");

        palette.Entries.Select(entry => entry.Name).ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Select(command => command.Name));
        foreach (var command in SlashCommandCatalogue.Shipped.ListAll())
        {
            var entry = palette.Entries.Single(candidate => string.Equals(candidate.Name, command.Name, StringComparison.Ordinal));
            if (command.Status == SlashCommandStatus.Unavailable)
            {
                entry.IsEnabled.ShouldBeFalse($"/{command.Name}");
                entry.DisabledReason.ShouldBe(command.DisabledReason);
                entry.Source.ShouldBe(SlashPaletteSource.Cursor);
            }
            else if (command.Kind == SlashHandlerKind.Skill)
            {
                entry.IsEnabled.ShouldBeFalse("no SKILL.md of that name was found");
                entry.DisabledReason.ShouldBe(SlashPaletteCopy.NoSkillInstalled(command.Name));
            }
            else
            {
                entry.IsEnabled.ShouldBeTrue($"/{command.Name}");
                entry.Source.ShouldBe(SlashPaletteSource.OmpOs);
            }
        }
    }

    private static string GroupLabel(SlashCommandGroup group) => group switch
    {
        SlashCommandGroup.Cli => "CLI",
        SlashCommandGroup.Chat => "Chat",
        _ => "Built-in skill",
    };

    private static string ExpectedHandler(SlashCommandDescriptor command)
    {
        switch (command.Kind)
        {
            case SlashHandlerKind.NativeAction:
                return $"`{command.HandlerId}{(command.ActionArgument is null ? string.Empty : " " + command.ActionArgument)}`";
            case SlashHandlerKind.OmpDelegated:
                var sent = command.EngineCommand ?? command.Name;
                var omp = command.HandlerId["omp.".Length..];
                return string.Equals(sent, omp, StringComparison.Ordinal) ? $"`/{sent}`" : $"`/{sent} (omp /{omp})`";
            case SlashHandlerKind.Skill:
                return $"`SKILL.md named {command.Name}`";
            case SlashHandlerKind.CloudHandoff:
                return "`cloud.handoff`";
            default:
                return "-";
        }
    }

    private static List<DocumentedRow> ReadDocumentedRows()
    {
        var lines = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "reference", "slash-commands.md"));
        var start = Array.FindIndex(lines, static line => string.Equals(line, "## Tokens and aliases", StringComparison.Ordinal));
        start.ShouldBeGreaterThan(-1, "the documented table has a heading 'Tokens and aliases'");
        var rows = new List<DocumentedRow>();
        for (var index = start + 1; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
        {
            if (!lines[index].StartsWith("| `/", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = lines[index].Split('|').Select(static cell => cell.Trim()).ToArray();
            cells.Length.ShouldBe(9, $"table row {lines[index]} has seven cells");
            rows.Add(new DocumentedRow(cells[1].Trim('`')[1..], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7]));
        }

        return rows;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"The repository root (OmpOS.slnx) was not found above {AppContext.BaseDirectory}.");
    }

    private sealed record DocumentedRow(string Token, string Group, string Cursor, string Kind, string Handler, string Status, string Reason);

    private sealed class AllowAllHost : ISlashActionHost
    {
        public bool CanRun(string actionId) => true;

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken) => Task.FromResult(SlashActionResult.Completed());
    }
}
