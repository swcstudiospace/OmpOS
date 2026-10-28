using Microsoft.Extensions.Options;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Reads the effective limits and options of the running API as read-only values. It copies names and numbers only: no connection string, no path, no key.</summary>
internal sealed class RuntimeSettingsReader
{
    private readonly IOptions<ApiOptions> _api;
    private readonly IOptions<HarnessOptions> _harness;
    private readonly IOptions<SessionsOptions> _sessions;
    private readonly IOptions<CoordinationOptions> _coordination;
    private readonly IOptions<WorkspaceSettingsOptions> _workspaceSettings;
    private readonly WorkspaceAccess _access;

    /// <summary>Initializes a new instance of the <see cref="RuntimeSettingsReader"/> class.</summary>
    /// <param name="api">The API limits.</param>
    /// <param name="harness">The harness options.</param>
    /// <param name="sessions">The session store options.</param>
    /// <param name="coordination">The coordination store options.</param>
    /// <param name="workspaceSettings">The workspace-reading options.</param>
    /// <param name="access">What the endpoints may read.</param>
    public RuntimeSettingsReader(
        IOptions<ApiOptions> api,
        IOptions<HarnessOptions> harness,
        IOptions<SessionsOptions> sessions,
        IOptions<CoordinationOptions> coordination,
        IOptions<WorkspaceSettingsOptions> workspaceSettings,
        WorkspaceAccess access)
    {
        _api = api;
        _harness = harness;
        _sessions = sessions;
        _coordination = coordination;
        _workspaceSettings = workspaceSettings;
        _access = access;
    }

    /// <summary>Reads the values now.</summary>
    /// <returns>The values.</returns>
    public RuntimeSettingsResponse Read()
    {
        var api = _api.Value;
        return new RuntimeSettingsResponse
        {
            HarnessAdapter = _harness.Value.Adapter,
            SessionStore = _sessions.Value.Store,
            CoordinationStore = _coordination.Value.Store,
            RunLeaseSeconds = CoordinationDefaults.RunLeaseTimeToLive.TotalSeconds,
            RunLeaseRenewSeconds = CoordinationDefaults.RunLeaseRenewInterval.TotalSeconds,
            MaxPromptLength = api.MaxPromptLength,
            MaxRequestBodyBytes = api.MaxRequestBodyBytes,
            PromptsPerMinute = api.RateLimit.PromptsPerMinute,
            SessionsPerMinute = api.RateLimit.SessionsPerMinute,
            WorkspaceRequestsPerMinute = _workspaceSettings.Value.RequestsPerMinute,
            WorkspaceConfigured = _access.ExistingRoot is not null,
            IncludesUserScope = _access.IncludesUserScope,
            MaxWorkspaceFileBytes = WorkspaceFileReader.MaxFileBytes,
        };
    }
}
