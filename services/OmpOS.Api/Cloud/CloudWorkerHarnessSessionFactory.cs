using Microsoft.Extensions.Options;
using OmpOS.Api.Cloud.Options;
using OmpOS.Api.Harness;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Sessions;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The <see cref="IHarnessSessionFactory"/> that sends the runs of cloud agents to their workers. A run of a session whose id carries the cloud prefix
/// (<see cref="CloudSessionIds"/>) gets a <see cref="CloudAgentHarnessSession"/>, which connects an <see cref="AcpWebSocketTransport"/> to the worker through
/// the same ACP session code the local harness uses; every other run goes to the wrapped factory unchanged. A cloud session is never run locally: when
/// its agent cannot be found or woken, the run fails. The decision needs no store lookup, so every API instance makes it the same way.
/// </summary>
internal sealed class CloudWorkerHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly IHarnessSessionFactory _inner;
    private readonly IServiceProvider _services;

    /// <summary>Initializes a new instance of the <see cref="CloudWorkerHarnessSessionFactory"/> class.</summary>
    /// <param name="inner">The factory for sessions that are not cloud agents'.</param>
    /// <param name="services">The service provider; the cloud services are resolved on use so a host with cloud off never builds them.</param>
    public CloudWorkerHarnessSessionFactory(IHarnessSessionFactory inner, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(services);
        _inner = inner;
        _services = services;
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt) => _inner.CreateSession(prompt);

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt, string sessionId, string runId)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (!CloudSessionIds.IsCloud(sessionId))
        {
            return _inner.CreateSession(prompt, sessionId, runId);
        }

        var options = _services.GetRequiredService<IOptionsMonitor<CloudOptions>>().CurrentValue;
        if (!options.Enabled)
        {
            throw new ProcessStartHarnessException("Cloud agents are disabled on this server.", "cloud_disabled");
        }

        var resolver = new SessionPermissionResolver(
            _services.GetRequiredService<ISessionRepository>(),
            _services.GetRequiredService<SessionService>(),
            sessionId,
            runId,
            AcpHarnessSessionFactory.PermissionDecisionTimeout,
            SessionPermissionResolver.DefaultRegistrationTimeout);
        return new CloudAgentHarnessSession(
            _services.GetRequiredService<CloudAgentRuntime>(),
            _services.GetRequiredService<WorkerTokenIssuer>(),
            sessionId,
            resolver,
            new AcpApprovalPolicy(options.Worker.ApprovalKinds),
            AcpHarnessSessionFactory.PermissionDecisionTimeout,
            _services.GetRequiredService<ILoggerFactory>());
    }
}
