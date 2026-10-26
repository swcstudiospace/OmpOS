using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings.Customize;

/// <summary>The Commands page over the real slash command registry.</summary>
public sealed class SlashCommandsSettingsSourceTests
{
    private static SlashCommandRegistry Registry() => new(
    [
        new SlashCommandDescriptor
        {
            Name = "side",
            Description = "Open a side chat.",
            Platforms = CommandPlatforms.Linux | CommandPlatforms.MacOS | CommandPlatforms.Windows,
            HandlerId = "chats.side.open",
            Arguments =
            [
                new SlashCommandArgument { Name = "topic", IsRequired = true, Description = "What to ask." },
                new SlashCommandArgument { Name = "model", IsRequired = false, Description = "Which model." },
            ],
        },
        new SlashCommandDescriptor
        {
            Name = "fork",
            Description = "Fork the chat.",
            Platforms = CommandPlatforms.Web,
            HandlerId = "chats.fork",
            DisabledReason = "Needs a completed prompt.",
        },
    ]);

    /// <summary>Every registered command is listed with its arguments in the title, the platforms as origin and the disabled reason in the description.</summary>
    [Fact]
    public async Task ListsEveryRegisteredCommand()
    {
        var page = new CommandsSettingsPage(new SlashCommandsSettingsSource(Registry()));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(CustomizePageState.Ready);
        page.Entries.Select(entry => entry.Title).ShouldBe(["/fork", "/side <topic> [model]"]);
        page.Entries.ShouldAllBe(entry => entry.Scope == CustomizeScope.BuiltIn);
        var side = page.Entries.Single(entry => entry.Title.StartsWith("/side", StringComparison.Ordinal));
        side.Origin.ShouldBe("Slash command, available on Linux, Windows, macOS");
        side.Description.ShouldBe("Open a side chat.");
        var fork = page.Entries.Single(entry => string.Equals(entry.Title, "/fork", StringComparison.Ordinal));
        fork.Origin.ShouldBe("Slash command, available on Web");
        fork.Description.ShouldBe("Fork the chat. Disabled: Needs a completed prompt.");
    }

    /// <summary>The shipped catalogue is listed as it is, and an empty registry gives the empty state.</summary>
    [Fact]
    public async Task ShippedCatalogueAndEmptyRegistry()
    {
        var token = TestContext.Current.CancellationToken;
        var shipped = new CommandsSettingsPage(new SlashCommandsSettingsSource(SlashCommandCatalogue.Shipped));
        await shipped.LoadAsync(token).ConfigureAwait(true);
        shipped.Entries.Count.ShouldBe(SlashCommandCatalogue.Shipped.ListAll().Count);

        var empty = new CommandsSettingsPage(new SlashCommandsSettingsSource(new SlashCommandRegistry([])));
        await empty.LoadAsync(token).ConfigureAwait(true);
        empty.State.ShouldBe(CustomizePageState.Empty);
        empty.EmptyText.ShouldBe(CustomizeCopy.CommandsEmpty);
    }

    /// <summary>A command that is available on no platform says so.</summary>
    [Fact]
    public void NoPlatformIsSaid()
    {
        CustomizeCopy.CommandOrigin(CommandPlatforms.None).ShouldBe("Slash command, available on no platform");
        CustomizeCopy.CommandOrigin(CommandPlatforms.Ios | CommandPlatforms.Android).ShouldBe("Slash command, available on iOS, Android");
    }
}
