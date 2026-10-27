using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>Slash-command registry: listing, prefix matching, suggestions, availability, and input parsing (VX-02).</summary>
public sealed class SlashCommandRegistryTests
{
    /// <summary>The test catalogue, registered in an order that is not sorted, so the registry must do the ordering.</summary>
    private static SlashCommandRegistry TestCatalogue() => new(
    [
        Command("resume"),
        Command("rewind"),
        Command("review"),
        Command("rewrite"),
        Command("reset"),
        Command("clear"),
        Command("fork", "Needs a completed prompt; run one first."),
    ]);

    /// <summary>
    /// The shipped catalogue holds every documented Cursor token: a token OmpOS cannot run resolves to Disabled with its reason, a token it runs resolves to Available with its handler,
    /// and a name that Cursor does not document is unknown.
    /// </summary>
    [Fact]
    public void ShippedCatalogueResolvesEveryDocumentedTokenAndNothingElse()
    {
        SlashCommandCatalogue.Shipped.ListAll().Count.ShouldBe(92);
        SlashCommandCatalogue.Shipped.MatchByPrefix("si").Select(command => command.Name).ShouldBe(["side"]);
        SlashCommandCatalogue.Shipped.MatchByPrefix("si").Select(command => command.HandlerId).ShouldBe(["chats.side.open"]);
        SlashCommandCatalogue.Shipped.Resolve("resume").ShouldBeOfType<SlashCommandDisabled>().Reason.ShouldContain("agent rail", Case.Sensitive);
        SlashCommandCatalogue.Shipped.Resolve("rename").ShouldBeOfType<SlashCommandAvailable>().Command.HandlerId.ShouldBe("omp.rename");
        SlashCommandCatalogue.Shipped.Resolve("not-a-cursor-token").ShouldBeOfType<SlashCommandUnknown>().Suggestions.ShouldBeEmpty();
        SlashCommandCatalogue.Shipped.Resolve("sid").ShouldBeOfType<SlashCommandUnknown>().Suggestions[0].ShouldBe("side");
        SlashCommandCatalogue.Shipped.Resolve("side").ShouldBeOfType<SlashCommandAvailable>().Command.HandlerId.ShouldBe("chats.side.open");
    }

    /// <summary>A command marked unavailable must say why, and a command of the Unavailable handler kind must be marked unavailable.</summary>
    [Fact]
    public void RegistryRefusesAnUnavailableCommandWithoutAReasonOrAKindThatContradictsItsStatus()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("gone") with { Status = SlashCommandStatus.Unavailable }]));
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("gone") with { Kind = SlashHandlerKind.Unavailable, DisabledReason = "Retired." }]));
        Should.NotThrow(() => new SlashCommandRegistry([Command("gone", "Retired.") with { Kind = SlashHandlerKind.Unavailable, Status = SlashCommandStatus.Unavailable }]));
    }

    /// <summary>ListAll returns the commands in ordinal name order, whatever order the registry was built in.</summary>
    [Fact]
    public void ListAllReturnsNamesInOrdinalOrder()
    {
        TestCatalogue().ListAll().Select(command => command.Name)
            .ShouldBe(["clear", "fork", "reset", "resume", "review", "rewind", "rewrite"]);
    }

    /// <summary>A prefix returns the matching commands in ordinal order; the empty prefix returns all; the match is case-sensitive.</summary>
    [Fact]
    public void MatchByPrefixOrdersByNameAndIsCaseSensitive()
    {
        var catalogue = TestCatalogue();

        catalogue.MatchByPrefix("re").Select(command => command.Name)
            .ShouldBe(["reset", "resume", "review", "rewind", "rewrite"]);
        catalogue.MatchByPrefix("rew").Select(command => command.Name).ShouldBe(["rewind", "rewrite"]);
        catalogue.MatchByPrefix(string.Empty).Select(command => command.Name).ShouldBe(catalogue.ListAll().Select(command => command.Name));
        catalogue.MatchByPrefix("RE").ShouldBeEmpty();
    }

    /// <summary>An enabled command resolves to Available with its descriptor, including its platform flags.</summary>
    [Fact]
    public void EnabledCommandResolvesToAvailable()
    {
        var resolution = TestCatalogue().Resolve("clear").ShouldBeOfType<SlashCommandAvailable>();

        resolution.Command.Name.ShouldBe("clear");
        resolution.Command.Platforms.ShouldBe(CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS);
    }

    /// <summary>A disabled command resolves to Disabled and reports the reason exactly as it was stored.</summary>
    [Fact]
    public void DisabledCommandReportsItsStoredReason()
    {
        var resolution = TestCatalogue().Resolve("fork").ShouldBeOfType<SlashCommandDisabled>();

        resolution.Reason.ShouldBe("Needs a completed prompt; run one first.");
        resolution.Command.Name.ShouldBe("fork");
    }

    /// <summary>An unregistered name resolves to Unknown and suggests the registered name within two edits.</summary>
    [Fact]
    public void UnknownNameSuggestsTheCloseRegisteredName()
    {
        var resolution = TestCatalogue().Resolve("rewid").ShouldBeOfType<SlashCommandUnknown>();

        resolution.Name.ShouldBe("rewid");
        resolution.Suggestions.ShouldBe(["rewind"]);
    }

    /// <summary>Suggestions are bounded to three names, prefix matches first, then the nearer name, then ordinal order.</summary>
    [Fact]
    public void SuggestionsAreBoundedAndNearestFirst()
    {
        // All five "re" names are prefix matches. Distances from "re": reset 3; resume, review, rewind 4; rewrite 5.
        TestCatalogue().Suggest("re").ShouldBe(["reset", "resume", "review"]);
    }

    /// <summary>A name that is neither a prefix match nor within two edits gets no suggestion.</summary>
    [Fact]
    public void FarAwayNameGetsNoSuggestion()
    {
        TestCatalogue().Suggest("zzzzzz").ShouldBeEmpty();
        TestCatalogue().Resolve("zzzzzz").ShouldBeOfType<SlashCommandUnknown>().Suggestions.ShouldBeEmpty();
    }

    /// <summary>Arguments are returned in the order the descriptor lists them.</summary>
    [Fact]
    public void ArgumentsKeepTheirDeclaredOrder()
    {
        var registry = new SlashCommandRegistry(
        [
            new SlashCommandDescriptor
            {
                Name = "move",
                Description = "Moves a file.",
                Platforms = CommandPlatforms.Web,
                HandlerId = "test.move",
                Arguments =
                [
                    new SlashCommandArgument { Name = "source", IsRequired = true, Description = "File to move." },
                    new SlashCommandArgument { Name = "target", IsRequired = false, Description = "Where to move it." },
                ],
            },
        ]);

        var command = registry.Resolve("move").ShouldBeOfType<SlashCommandAvailable>().Command;

        command.Arguments.Select(argument => argument.Name).ShouldBe(["source", "target"]);
        command.Arguments.Select(argument => argument.IsRequired).ShouldBe([true, false]);
    }

    /// <summary>Input that starts with a slash command is split into the command name and its argument text.</summary>
    [Fact]
    public void ParseSplitsCommandNameAndArgumentText()
    {
        SlashCommandRegistry.Parse("/resume rest of the line").ShouldBe(new SlashCommandInvocation("resume", "rest of the line"));
        SlashCommandRegistry.Parse("/resume").ShouldBe(new SlashCommandInvocation("resume", string.Empty));
    }

    /// <summary>Text that does not start with a slash, or a slash that is not at the start, is not a command.</summary>
    [Fact]
    public void ParseRejectsTextThatDoesNotStartWithASlash()
    {
        SlashCommandRegistry.Parse("resume now").ShouldBeNull();
        SlashCommandRegistry.Parse("ask /resume later").ShouldBeNull();
        SlashCommandRegistry.Parse(string.Empty).ShouldBeNull();
    }

    /// <summary>A slash command inside a code fence is not a command.</summary>
    [Fact]
    public void ParseRejectsASlashCommandInsideACodeFence()
    {
        SlashCommandRegistry.Parse("```\n/resume\n```").ShouldBeNull();
    }

    /// <summary>A name the recognizer cannot read back as the same name is refused when the registry is built.</summary>
    [Fact]
    public void RegistryRefusesANameTheRecognizerCannotRead()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("Resume")]));
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("two words")]));
    }

    /// <summary>Two commands with the same name are refused when the registry is built.</summary>
    [Fact]
    public void RegistryRefusesDuplicateNames()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("clear"), Command("clear")]));
    }

    /// <summary>A disabled reason that is blank is refused, so a blank string never reads as a reason.</summary>
    [Fact]
    public void RegistryRefusesABlankDisabledReason()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("fork", " ")]));
    }

    /// <summary>A blank description is refused, as a blank handler id or disabled reason is.</summary>
    [Fact]
    public void RegistryRefusesABlankDescription()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("clear") with { Description = " " }]));
    }

    /// <summary>A platform bit that CommandPlatforms does not define is refused when the registry is built.</summary>
    [Fact]
    public void RegistryRefusesAnUndefinedPlatformBit()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry([Command("clear") with { Platforms = (CommandPlatforms)64 }]));
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry(
        [
            Command("clear") with { Platforms = CommandPlatforms.Linux | (CommandPlatforms)64 },
        ]));
    }

    /// <summary>The registry keeps its own copy of each argument list, so a later change to the caller's list does not reach it.</summary>
    [Fact]
    public void RegistryKeepsACopyOfTheArgumentList()
    {
        var arguments = new List<SlashCommandArgument>
        {
            new SlashCommandArgument { Name = "source", IsRequired = true, Description = "File to move." },
        };
        var registry = new SlashCommandRegistry([Command("move") with { Arguments = arguments }]);

        arguments.Add(new SlashCommandArgument { Name = "target", IsRequired = false, Description = "Where to move it." });
        arguments[0] = new SlashCommandArgument { Name = "renamed", IsRequired = true, Description = "Changed." };

        registry.Resolve("move").ShouldBeOfType<SlashCommandAvailable>().Command.Arguments
            .Select(argument => argument.Name).ShouldBe(["source"]);
    }

    /// <summary>Two arguments of one command with the same name are refused when the registry is built.</summary>
    [Fact]
    public void RegistryRefusesDuplicateArgumentNames()
    {
        Should.Throw<ArgumentException>(() => new SlashCommandRegistry(
        [
            new SlashCommandDescriptor
            {
                Name = "move",
                Description = "Moves a file.",
                Platforms = CommandPlatforms.Web,
                HandlerId = "test.move",
                Arguments =
                [
                    new SlashCommandArgument { Name = "path", IsRequired = true, Description = "A path." },
                    new SlashCommandArgument { Name = "path", IsRequired = false, Description = "Another path." },
                ],
            },
        ]));
    }

    private static SlashCommandDescriptor Command(string name, string? disabledReason = null) => new()
    {
        Name = name,
        Description = $"Runs {name}.",
        Platforms = CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS,
        DisabledReason = disabledReason,
        HandlerId = $"test.{name}",
    };
}
