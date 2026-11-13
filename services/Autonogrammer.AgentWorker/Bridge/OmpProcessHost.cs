using System.Collections.Concurrent;
using Autonogrammer.AgentWorker.Configuration;
using Autonogrammer.AgentWorker.Hooks;
using Autonogrammer.AgentWorker.Security;
using Autonogrammer.Harness.Acp;
using Microsoft.Extensions.Options;

namespace Autonogrammer.AgentWorker.Bridge;

/// <summary>
/// Starts <c>omp acp</c> for attached sessions, one child per session subject. The child runs through <see cref="AcpStdioTransport"/>
/// (the process abstraction of the harness layer) in the session's directory under the workspace root, with the worker's environment and
/// the configured arguments. Nothing secret is put in argv: provider credentials reach omp only through the inherited environment.
/// </summary>
internal sealed class OmpProcessHost
{
    private readonly WorkerOptions _options;
    private readonly WorkerSecretRedactor _redactor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CloudHooksFactory _hooks;
    private readonly ConcurrentDictionary<string, Claim> _attached = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="OmpProcessHost"/> class.</summary>
    /// <param name="options">The validated worker options.</param>
    /// <param name="redactor">Scrubs secrets from the child's stderr lines.</param>
    /// <param name="loggerFactory">Creates the loggers of the transport and the attachments.</param>
    /// <param name="hooks">Creates the hooks of a connection; it creates none when the worker runs no hooks.</param>
    public OmpProcessHost(IOptions<WorkerOptions> options, WorkerSecretRedactor redactor, ILoggerFactory loggerFactory, CloudHooksFactory hooks)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(hooks);
        _options = options.Value;
        _redactor = redactor;
        _loggerFactory = loggerFactory;
        _hooks = hooks;
    }

    /// <summary>Gets how many sessions are attached at this moment.</summary>
    public int AttachedCount => _attached.Count;

    /// <summary>Claims a session subject and prepares its omp child. The child is not started until <see cref="OmpAttachment.StartAsync"/>.</summary>
    /// <param name="sessionId">The validated session subject.</param>
    /// <param name="ownerDir">The owner directory name from the token's <c>owner_dir</c> claim; the session runs under it.</param>
    /// <param name="bearerToken">The connection's bearer token, redacted from the child's stderr lines while attached.</param>
    /// <param name="consent">What the token carried about hook consent, or null for a token without it. Only used when the worker runs hooks.</param>
    /// <returns>The attachment, or <see langword="null"/> when the session already has an attached connection.</returns>
    /// <exception cref="ArgumentException"><paramref name="ownerDir"/> is not a well-formed owner directory name.</exception>
    public OmpAttachment? TryAttach(string sessionId, string ownerDir, string bearerToken, HooksConsentRead? consent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(bearerToken);
        if (!OwnerDirectory.IsValid(ownerDir))
        {
            throw new ArgumentException("The owner directory must be 64 lowercase hex characters.", nameof(ownerDir));
        }

        bool claimed;
        lock (_gate)
        {
            claimed = _attached.TryAdd(sessionId, new Claim(Path.Combine(_options.WorkspaceRoot, ownerDir, sessionId), new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)));
        }

        if (!claimed)
        {
            return null;
        }

        var workingDirectory = Path.Combine(_options.WorkspaceRoot, ownerDir, sessionId);
        var hooks = _hooks.Create(sessionId, workingDirectory, consent ?? HooksConsentRead.Absent);
        return new OmpAttachment(this, sessionId, workingDirectory, launch => CreateTransport(workingDirectory, launch), hooks, _redactor.Track(bearerToken), _redactor, _loggerFactory.CreateLogger<OmpAttachment>());
    }

    private AcpStdioTransport CreateTransport(string workingDirectory, CloudHooksLaunch launch) =>
        new(
            _options.Omp.Path,
            workingDirectory,
            [.. _options.Omp.ExtraArguments, .. launch.ExtraArguments],
            new Dictionary<string, string>(launch.Environment, StringComparer.Ordinal),
            _loggerFactory.CreateLogger<AcpStdioTransport>());

    /// <summary>Frees a session subject so a new connection may attach.</summary>
    /// <param name="sessionId">The session subject.</param>
    internal void Release(string sessionId)
    {
        if (_attached.TryRemove(sessionId, out var claim))
        {
            claim.Done.TrySetResult();
        }
    }

    /// <summary>Returns a task that completes when the session's current claim, if any, is released: its omp child is gone and the subject is free again.</summary>
    /// <param name="sessionId">The session subject.</param>
    /// <returns>A task that is already complete when the session is not attached.</returns>
    public Task WaitForReleaseAsync(string sessionId) =>
        _attached.TryGetValue(sessionId, out var claim) ? claim.Done.Task : Task.CompletedTask;

    /// <summary>Gets whether any attached session's working directory is <paramref name="directory"/> or lies under it.</summary>
    /// <param name="directory">The directory to test, as a full or relative path.</param>
    /// <returns><see langword="true"/> when an attached session runs inside the directory.</returns>
    public bool IsAnyAttachedUnder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root + Path.DirectorySeparatorChar;
        return _attached.Values.Any(claim =>
        {
            var working = Path.TrimEndingDirectorySeparator(Path.GetFullPath(claim.WorkingDirectory));
            return string.Equals(working, root, comparison) || working.StartsWith(prefix, comparison);
        });
    }

    /// <summary>Runs <paramref name="action"/> while no session can attach, so a check and the work that depends on it see the same attachments.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="action">The work to run under the gate. It must not call <see cref="TryAttach"/>, which takes the same gate.</param>
    /// <returns>The result of <paramref name="action"/>.</returns>
    public T RunExclusive<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            return action();
        }
    }

    private sealed record Claim(string WorkingDirectory, TaskCompletionSource Done);
}
