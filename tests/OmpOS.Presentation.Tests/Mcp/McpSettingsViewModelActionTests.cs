using OmpOS.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Mcp;

/// <summary>The MCP page actions: switching, the consent prompt, connection checks, add-from-template and the open-file seam, each writing through the source and reading the list again.</summary>
public sealed class McpSettingsViewModelActionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(McpSettingsViewModel Model, ScriptedMcpSource Source)> LoadedAsync(ScriptedMcpSource source)
    {
        var model = new McpSettingsViewModel(source);
        await model.LoadAsync(Token).ConfigureAwait(true);
        return (model, source);
    }

    [Fact]
    public async Task TheSwitchButtonTurnsAServerOffAndOnAndTheRowFollows()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(McpEntries.Ready("files"))).ConfigureAwait(true);
        var row = model.Rows.Single();
        row.ToggleText.ShouldBe("Switch off");

        await ((McpActionCommand)row.ToggleCommand).ExecuteAsync().ConfigureAwait(true);

        source.Calls.ShouldBe(["load", "disable files"]);
        model.Rows.Single().StatusText.ShouldBe("Off");
        model.Rows.Single().ToggleText.ShouldBe("Switch on");
        model.Rows.Single().StatusDetail.ShouldBe("Switched off in OmpOS.");

        await ((McpActionCommand)model.Rows.Single().ToggleCommand).ExecuteAsync().ConfigureAwait(true);

        source.Calls.Last().ShouldBe("enable files");
        model.Rows.Single().StatusText.ShouldBe("Ready");
    }

    [Fact]
    public async Task AFailedWriteIsReportedInTheActionFailureAndTheListIsReadAgain()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready("files")) { WriteFailure = "The MCP state file could not be written." };
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);

        await model.SetEnabledAsync("files", false, Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("The MCP state file could not be written.");
        model.HasActionFailure.ShouldBeTrue();
        model.Rows.Single().IsEnabled.ShouldBeTrue();
        model.State.ShouldBe(McpViewState.Success);
        model.IsBusy.ShouldBeFalse();

        source.WriteFailure = null;
        await model.SetEnabledAsync("files", false, Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheConsentPromptShowsTheExactCommandAndRecordsNothingUntilApproved()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(McpEntries.NeedsConsent("tool"))).ConfigureAwait(true);

        await ((McpActionCommand)model.Rows.Single().ReviewCommand).ExecuteAsync().ConfigureAwait(true);

        var prompt = model.PendingConsent.ShouldNotBeNull();
        prompt.Summary.ShouldBe("tool (stdio server, .cursor/mcp.json)");
        prompt.CommandText.ShouldBe("node ./tool.js --serve");
        prompt.EnvironmentText.ShouldBe("Environment: TOOL_TOKEN");
        prompt.ApproveName.ShouldBe("Approve server tool");
        model.HasPendingConsent.ShouldBeTrue();
        source.Calls.ShouldBe(["load"]);

        await model.ApproveCommand.ExecuteAsync().ConfigureAwait(true);

        source.Calls.ShouldBe(["load", "grant tool"]);
        model.PendingConsent.ShouldBeNull();
        model.Rows.Single().StatusText.ShouldBe("Ready");
        model.Rows.Single().ShowRevoke.ShouldBeTrue();
        model.State.ShouldBe(McpViewState.Success);
    }

    [Fact]
    public async Task CancellingTheConsentPromptWritesNothing()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(McpEntries.NeedsConsent("tool"))).ConfigureAwait(true);
        model.RequestConsent("tool");

        await model.CancelConsentCommand.ExecuteAsync().ConfigureAwait(true);

        model.PendingConsent.ShouldBeNull();
        source.Calls.ShouldBe(["load"]);
        model.Rows.Single().StatusText.ShouldBe("Needs approval");
    }

    [Fact]
    public async Task AFailedApprovalKeepsThePromptOpenWithTheReason()
    {
        var source = new ScriptedMcpSource(McpEntries.NeedsConsent("tool"));
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);
        model.RequestConsent("tool");
        source.WriteFailure = "The MCP state file could not be written.";

        await model.ConfirmConsentAsync(Token).ConfigureAwait(true);

        model.PendingConsent.ShouldNotBeNull();
        model.ActionFailure.ShouldBe("The MCP state file could not be written.");
        model.Rows.Single().StatusText.ShouldBe("Needs approval");
    }

    [Fact]
    public async Task ApprovingWithoutAPromptAndPromptingForAnUnknownServerDoNothing()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(McpEntries.NeedsConsent("tool"))).ConfigureAwait(true);

        await model.ConfirmConsentAsync(Token).ConfigureAwait(true);
        model.RequestConsent("missing");

        model.PendingConsent.ShouldBeNull();
        source.Calls.ShouldBe(["load"]);
    }

    [Fact]
    public async Task ThePromptClosesWhenItsServerNoLongerNeedsConsent()
    {
        var source = new ScriptedMcpSource(McpEntries.NeedsConsent("tool"));
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);
        model.RequestConsent("tool");

        await source.GrantConsentAsync("tool", Token).ConfigureAwait(true);
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);

        model.PendingConsent.ShouldBeNull();
    }

    [Fact]
    public async Task WithdrawingApprovalAsksForConsentAgain()
    {
        var source = new ScriptedMcpSource(McpEntries.NeedsConsent("tool"));
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);
        await source.GrantConsentAsync("tool", Token).ConfigureAwait(true);
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);

        await ((McpActionCommand)model.Rows.Single().RevokeCommand).ExecuteAsync().ConfigureAwait(true);

        model.Rows.Single().StatusText.ShouldBe("Needs approval");
        source.Calls.Last().ShouldBe("revoke tool");
    }

    [Fact]
    public async Task ACheckShowsConnectingWhileItRunsThenTheResult()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready("files"));
        source.HoldCheck();
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);

        var running = ((McpActionCommand)model.Rows.Single().CheckCommand).ExecuteAsync();

        model.Rows.Single().StatusText.ShouldBe("Connecting");
        model.IsBusy.ShouldBeTrue();
        source.Calls.ShouldBe(["load", "check files"]);

        source.CompleteCheck();
        await running.ConfigureAwait(true);

        model.Rows.Single().StatusText.ShouldBe("Connected");
        model.Rows.Single().ToolCountText.ShouldBe("2 tools");
        model.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task AddingATemplateUsesTheTypedNameReportsTheFileAndClearsTheName()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource()).ConfigureAwait(true);
        model.TemplateName = "  starter ";

        await model.AddStdioCommand.ExecuteAsync().ConfigureAwait(true);

        source.Calls.Last().ShouldBe("template Stdio starter");
        model.ActionResult.ShouldBe("Added starter to .ompos/mcp.json. It is switched off until you edit it and switch it on.");
        model.HasActionResult.ShouldBeTrue();
        model.TemplateName.ShouldBeEmpty();
        model.Rows.Single().Name.ShouldBe("starter");
        model.Rows.Single().StatusText.ShouldBe("Off");
        model.State.ShouldBe(McpViewState.Success);

        model.TemplateName = "remote";
        await model.AddHttpCommand.ExecuteAsync().ConfigureAwait(true);

        source.Calls.Last().ShouldBe("template Http remote");
    }

    [Fact]
    public async Task AFailedTemplateKeepsTheNameAndShowsTheReason()
    {
        var source = new ScriptedMcpSource { WriteFailure = "A server named 'starter' is already configured in .cursor/mcp.json." };
        var (model, _) = await LoadedAsync(source).ConfigureAwait(true);
        model.TemplateName = "starter";

        await model.AddTemplateAsync(McpTemplateChoice.Stdio, Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("A server named 'starter' is already configured in .cursor/mcp.json.");
        model.TemplateName.ShouldBe("starter");
        model.ActionResult.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpeningAFileAsksTheSourceWithTheOriginTheRowAndGroupShow()
    {
        var (model, source) = await LoadedAsync(new ScriptedMcpSource(McpEntries.Ready("files"))).ConfigureAwait(true);

        await ((McpActionCommand)model.Rows.Single().OpenCommand).ExecuteAsync().ConfigureAwait(true);
        await ((McpActionCommand)model.Groups.Single().OpenCommand).ExecuteAsync().ConfigureAwait(true);

        source.Calls.Skip(1).ShouldBe(["open ~/.cursor/mcp.json", "open ~/.cursor/mcp.json"]);
        model.Groups.Single().OpenName.ShouldBe("Open file ~/.cursor/mcp.json");
    }
}
