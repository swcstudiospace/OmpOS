using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Services;
using OmpOS.Api.Services.WorkspaceSettings;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The read-only settings endpoints: runtime, plugins, network and automations.</summary>
public sealed class WorkspaceSettingsEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary><c>GET /settings/runtime</c> reports the configured limits, the run lease, the adapter and nothing else.</summary>
    [Fact]
    public async Task Runtime_ReportsTheEffectiveOptions_AndOnlyThose()
    {
        using var workspace = new TempWorkspace();
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Api:MaxPromptLength"] = "1234",
            ["Api:RateLimit:PromptsPerMinute"] = "7",
            ["Api:RateLimit:SessionsPerMinute"] = "9",
            ["Api:Workspace:RequestsPerMinute"] = "500",
        };
        using var factory = WorkspaceHost.Dev(workspace, config);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var root = document.RootElement;
        root.EnumerateObject().Select(static property => property.Name).ShouldBe(
            [
                "harnessAdapter", "sessionStore", "coordinationStore", "runLeaseSeconds", "runLeaseRenewSeconds", "maxPromptLength", "maxRequestBodyBytes",
                "promptsPerMinute", "sessionsPerMinute", "workspaceRequestsPerMinute", "workspaceConfigured", "includesUserScope", "maxWorkspaceFileBytes",
            ],
            ignoreOrder: true);
        root.GetProperty("harnessAdapter").GetString().ShouldBe("InMemory");
        root.GetProperty("sessionStore").GetString().ShouldBe("InMemory");
        root.GetProperty("coordinationStore").GetString().ShouldBe("InMemory");
        root.GetProperty("runLeaseSeconds").GetDouble().ShouldBe(30);
        root.GetProperty("runLeaseRenewSeconds").GetDouble().ShouldBe(10);
        root.GetProperty("maxPromptLength").GetInt32().ShouldBe(1234);
        root.GetProperty("maxRequestBodyBytes").GetInt64().ShouldBe(1_048_576);
        root.GetProperty("promptsPerMinute").GetInt32().ShouldBe(7);
        root.GetProperty("sessionsPerMinute").GetInt32().ShouldBe(9);
        root.GetProperty("workspaceRequestsPerMinute").GetInt32().ShouldBe(500);
        root.GetProperty("workspaceConfigured").GetBoolean().ShouldBeTrue();
        root.GetProperty("includesUserScope").GetBoolean().ShouldBeFalse();
        root.GetProperty("maxWorkspaceFileBytes").GetInt32().ShouldBe(1_048_576);
        WorkspaceHost.ShouldNotLeakPaths(body, workspace);
    }

    /// <summary>Without a workspace folder the runtime values say so.</summary>
    [Fact]
    public async Task Runtime_WithoutAWorkspace_SaysNotConfigured()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/runtime", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("workspaceConfigured").GetBoolean().ShouldBeFalse();
    }

    /// <summary>A host with no plugin inventory reports the check as unavailable and lists nothing.</summary>
    [Fact]
    public async Task Plugins_ByDefault_AreUnavailable()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/plugins", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("check").GetProperty("status").GetString().ShouldBe("unavailable");
        document.RootElement.GetProperty("check").GetProperty("summary").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("plugins").GetArrayLength().ShouldBe(0);
    }

    /// <summary>A host that registers a plugin check and an inventory has both reported.</summary>
    [Fact]
    public async Task Plugins_FromTheHostSeams_AreReported()
    {
        var check = new PluginCheckResult { Status = PluginCheckStatus.Degraded, Summary = "One plugin is missing." };
        WorkspacePluginInfo[] plugins =
        [
            new WorkspacePluginInfo { Id = "ompos-hooks", DisplayName = "Hooks", Role = "Runs hooks", Version = "1.2.3", Found = true, Detail = "Found." },
            new WorkspacePluginInfo { Id = "gsd", DisplayName = "GSD", Role = "Planning", Found = false, Detail = "Not installed." },
        ];
        using var factory = WorkspaceHost.Dev(null, more: services =>
        {
            services.AddSingleton<IWorkspacePluginCheck>(new StubPluginCheck(check));
            services.AddSingleton<IWorkspacePluginInventory>(new StubPluginInventory(plugins));
        });
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/plugins", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("check").GetProperty("status").GetString().ShouldBe("degraded");
        var rows = document.RootElement.GetProperty("plugins").EnumerateArray().ToArray();
        rows.Length.ShouldBe(2);
        rows[0].GetProperty("version").GetString().ShouldBe("1.2.3");
        rows[0].GetProperty("found").GetBoolean().ShouldBeTrue();
        rows[1].GetProperty("found").GetBoolean().ShouldBeFalse();
        rows[1].TryGetProperty("version", out _).ShouldBeFalse();
    }

    /// <summary>With nothing configured the network list is empty and honestly unchecked.</summary>
    [Fact]
    public async Task Network_ByDefault_IsUncheckedAndEmpty()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/network", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("checked").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("endpoints").GetArrayLength().ShouldBe(0);
    }

    /// <summary>A configured authority is listed by host and port only: no scheme, path or query reaches the answer.</summary>
    [Fact]
    public async Task Network_ListsAConfiguredAuthority_ByHostOnly()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Auth:Authority"] = "https://user:pass@login.example.com:8443/realms/secret?token=abc" };
        using var factory = WorkspaceHost.Dev(null, config);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/settings/network", Token).ConfigureAwait(true);

        var body = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = System.Text.Json.JsonDocument.Parse(body);
        document.RootElement.GetProperty("checked").GetBoolean().ShouldBeFalse();
        var endpoint = document.RootElement.GetProperty("endpoints").EnumerateArray().ShouldHaveSingleItem();
        endpoint.GetProperty("name").GetString().ShouldBe("Sign-in authority");
        endpoint.GetProperty("host").GetString().ShouldBe("login.example.com:8443");
        body.ShouldNotContain("pass", Case.Sensitive);
        body.ShouldNotContain("secret", Case.Sensitive);
        body.ShouldNotContain("token=abc", Case.Sensitive);
    }

    /// <summary>A host with no automation engine says so, with a reason.</summary>
    [Fact]
    public async Task Automations_ByDefault_AreUnavailable()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/automations", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("automations").GetArrayLength().ShouldBe(0);
    }

    /// <summary>A host that registers an automations provider has its automations listed with the last run and the pause.</summary>
    [Fact]
    public async Task Automations_FromTheHostSeam_AreListed()
    {
        var stamp = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
        WorkspaceAutomationInfo[] automations =
        [
            new WorkspaceAutomationInfo
            {
                Id = "nightly",
                Trigger = "schedule",
                Enabled = true,
                MaxRuntimeSeconds = 900,
                LastRun = new AutomationLastRunInfo { Outcome = "failed", Reason = "Timed out upstream.", RecordedAt = stamp },
                Pause = new AutomationPauseInfo { Guard = "loop", Reason = "Three failures.", HowToResume = "Resume it in the desktop app." },
            },
            new WorkspaceAutomationInfo { Id = "manual", Trigger = "manual", Enabled = false, MaxRuntimeSeconds = 60 },
        ];
        using var factory = WorkspaceHost.Dev(null, more: services => services.AddSingleton<IWorkspaceAutomationsProvider>(new StubAutomationsProvider(automations)));
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/automations", Token).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
        var rows = document.RootElement.GetProperty("automations").EnumerateArray().ToArray();
        rows.Length.ShouldBe(2);
        rows[0].GetProperty("id").GetString().ShouldBe("nightly");
        rows[0].GetProperty("maxRuntimeSeconds").GetDouble().ShouldBe(900);
        rows[0].GetProperty("lastRun").GetProperty("outcome").GetString().ShouldBe("failed");
        rows[0].GetProperty("pause").GetProperty("guard").GetString().ShouldBe("loop");
        rows[1].TryGetProperty("lastRun", out _).ShouldBeFalse();
        rows[1].TryGetProperty("pause", out _).ShouldBeFalse();
    }
}
