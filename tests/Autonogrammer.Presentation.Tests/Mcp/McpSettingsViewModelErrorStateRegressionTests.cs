using Autonogrammer.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mcp;

/// <summary>A failed reload holds the error state until a load succeeds, so an action that refreshes the page from the last snapshot cannot replace it.</summary>
public sealed class McpSettingsViewModelErrorStateRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnActionAfterAFailedReloadKeepsTheErrorState()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready("files"));
        var model = new McpSettingsViewModel(source);
        await model.LoadAsync(Token).ConfigureAwait(true);
        model.State.ShouldBe(McpViewState.Success);

        source.LoadFailure = "The state file is locked.";
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);
        await model.SetEnabledAsync("files", false, Token).ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Error);
        model.IsError.ShouldBeTrue();
        model.ShowList.ShouldBeFalse();
        model.LoadFailure.ShouldBe("The state file is locked.");
    }

    [Fact]
    public async Task ASuccessfulLoadAfterAFailedReloadLeavesTheErrorState()
    {
        var source = new ScriptedMcpSource(McpEntries.Ready("files"));
        var model = new McpSettingsViewModel(source);
        await model.LoadAsync(Token).ConfigureAwait(true);
        source.LoadFailure = "The state file is locked.";
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);

        source.LoadFailure = null;
        await model.ReloadCommand.ExecuteAsync().ConfigureAwait(true);

        model.State.ShouldBe(McpViewState.Success);
        model.LoadFailure.ShouldBeEmpty();
    }
}
