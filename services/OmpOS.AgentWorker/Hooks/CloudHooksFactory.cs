using Microsoft.Extensions.Options;
using OmpOS.AgentWorker.Configuration;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.AgentWorker.Hooks;

/// <summary>Creates the <see cref="CloudHooksSession"/> of a connection, or none when the worker runs no hooks (<c>Worker:Hooks:Enabled</c> is off, the default).</summary>
internal sealed class CloudHooksFactory
{
    private readonly WorkerOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILoggerFactory _loggers;
    private readonly IHookCommandRunner? _runner;

    /// <summary>Initializes a new instance of the <see cref="CloudHooksFactory"/> class.</summary>
    /// <param name="options">The validated worker options.</param>
    /// <param name="clock">The clock behind hook timestamps and timeouts.</param>
    /// <param name="loggers">Creates the sessions' loggers.</param>
    /// <param name="runner">Starts hook processes, or null (the default) for the system runner. A test passes a fake.</param>
    public CloudHooksFactory(IOptions<WorkerOptions> options, TimeProvider clock, ILoggerFactory loggers, IHookCommandRunner? runner = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(loggers);
        _options = options.Value;
        _clock = clock;
        _loggers = loggers;
        _runner = runner;
    }

    /// <summary>Creates the hooks of one connection. Nothing is read or started until the session's <see cref="CloudHooksSession.StartAsync"/>.</summary>
    /// <param name="sessionId">The session subject.</param>
    /// <param name="workspaceDirectory">The session directory the checkout lives in.</param>
    /// <param name="consent">What the connection's token carried.</param>
    /// <returns>The session, or null when hooks are off.</returns>
    public CloudHooksSession? Create(string sessionId, string workspaceDirectory, HooksConsentRead consent)
    {
        ArgumentNullException.ThrowIfNull(consent);
        var hooks = _options.Hooks;
        if (!hooks.Enabled)
        {
            return null;
        }

        var stateRoot = string.IsNullOrWhiteSpace(hooks.StateRoot)
            ? Path.Combine(_options.WorkspaceRoot, WorkerHooksOptions.DefaultStateDirectoryName)
            : hooks.StateRoot;
        return new CloudHooksSession(sessionId, workspaceDirectory, consent, hooks, stateRoot, _clock, _runner, _loggers);
    }
}
