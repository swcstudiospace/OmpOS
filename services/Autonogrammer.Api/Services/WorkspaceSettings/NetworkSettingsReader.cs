using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.WorkspaceSettings;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>
/// Lists the upstreams the API is configured to talk to (the sign-in authority and the session and coordination stores, when selected) by host only.
/// It makes no outbound call, so the answer is always <c>checked: false</c>. A reachability probe would be a request the caller triggers against
/// a host the operator named; this API does not send one.
/// </summary>
internal sealed class NetworkSettingsReader
{
    private const string UnparseableHost = "(configured, host not readable)";

    private readonly IOptions<ApiAuthOptions> _auth;
    private readonly IOptions<SessionsOptions> _sessions;
    private readonly IOptions<CoordinationOptions> _coordination;

    /// <summary>Initializes a new instance of the <see cref="NetworkSettingsReader"/> class.</summary>
    /// <param name="auth">The sign-in options.</param>
    /// <param name="sessions">The session store options.</param>
    /// <param name="coordination">The coordination store options.</param>
    public NetworkSettingsReader(IOptions<ApiAuthOptions> auth, IOptions<SessionsOptions> sessions, IOptions<CoordinationOptions> coordination)
    {
        _auth = auth;
        _sessions = sessions;
        _coordination = coordination;
    }

    /// <summary>Lists the configured upstreams.</summary>
    /// <returns>The upstreams, all unchecked.</returns>
    public NetworkSettingsResponse Read()
    {
        var endpoints = new List<NetworkEndpointInfo>();
        var authority = _auth.Value.Authority;
        if (!string.IsNullOrWhiteSpace(authority))
        {
            endpoints.Add(Endpoint("Sign-in authority", UpstreamHost.FromUrl(authority)));
        }

        var sessions = _sessions.Value;
        if (SessionsOptions.IsPostgresStore(sessions.Store))
        {
            endpoints.Add(Endpoint("Session store (PostgreSQL)", UpstreamHost.FromConnectionString(sessions.Postgres.ConnectionString)));
        }

        var coordination = _coordination.Value;
        if (CoordinationOptions.IsDragonflyStore(coordination.Store))
        {
            endpoints.Add(Endpoint("Coordination store (Dragonfly)", UpstreamHost.FromRedisConfiguration(coordination.Dragonfly.ConnectionString)));
        }

        return new NetworkSettingsResponse
        {
            Checked = false,
            Reason = "The API sends no request to check an upstream. The list shows what is configured, not whether it answers.",
            Endpoints = endpoints,
        };
    }

    private static NetworkEndpointInfo Endpoint(string name, string? host) => new() { Name = name, Host = host ?? UnparseableHost };
}
