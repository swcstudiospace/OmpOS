using System.Net;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>The Plugins page over <c>GET /settings/plugins</c>.</summary>
public sealed class WorkspacePluginsSettingsSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PluginsSettingsResponse Answer(PluginCheckStatus status, string summary, params WorkspacePluginInfo[] plugins) =>
        new() { Check = new PluginCheckResult { Status = status, Summary = summary }, Plugins = plugins };

    /// <summary>The rows map found and missing plugins, the version and the detail.</summary>
    [Fact]
    public async Task Rows_MapFoundAndMissingPlugins()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var answer = Answer(
            PluginCheckStatus.Degraded,
            "One plugin is missing.",
            new WorkspacePluginInfo { Id = "hooks", DisplayName = "Hooks", Role = "Runs hooks", Version = "1.2.3", Found = true, Detail = "Found." },
            new WorkspacePluginInfo { Id = "gsd", DisplayName = "GSD", Role = "Planning", Found = false, Detail = "Not installed." });
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, answer, WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspacePluginsSettingsSource(WorkspaceFixtures.Client(http));

        var values = await source.LoadAsync(Token).ConfigureAwait(true);

        values.Rows.Count.ShouldBe(2);
        values.Rows[0].IsFound.ShouldBeTrue();
        values.Rows[0].Version.ShouldBe("1.2.3");
        values.Rows[0].DisplayName.ShouldBe("Hooks");
        values.Rows[1].IsFound.ShouldBeFalse();
        values.Rows[1].Version.ShouldBeNull();
        values.Rows[1].Detail.ShouldBe("Not installed.");
    }

    /// <summary>An API with no inventory and no check is the page's unavailable state with the API's reason, not an empty list.</summary>
    [Fact]
    public async Task NoInventoryAndNoCheck_IsUnavailableWithTheReason()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, Answer(PluginCheckStatus.Unavailable, "This API has no plugin inventory."), WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspacePluginsSettingsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldBe("This API has no plugin inventory.");
    }

    /// <summary>A healthy check with no plugins is an empty list, not an error.</summary>
    [Fact]
    public async Task AHealthyCheckWithNoPlugins_IsEmpty()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var queued = WorkspaceFixtures.Json(HttpStatusCode.OK, Answer(PluginCheckStatus.Ok, "Nothing to check."), WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);
        handler.Enqueue(queued);
        var source = new WorkspacePluginsSettingsSource(WorkspaceFixtures.Client(http));

        var values = await source.LoadAsync(Token).ConfigureAwait(true);

        values.Rows.ShouldBeEmpty();
    }

    /// <summary>A refused call is the page's error state.</summary>
    [Fact]
    public async Task ARefusedCall_IsTheErrorState()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        using var refused = WorkspaceFixtures.Text(HttpStatusCode.TooManyRequests, string.Empty);
        handler.Enqueue(refused);
        var source = new WorkspacePluginsSettingsSource(WorkspaceFixtures.Client(http));

        var failure = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("Too many requests", Case.Sensitive);
    }
}
