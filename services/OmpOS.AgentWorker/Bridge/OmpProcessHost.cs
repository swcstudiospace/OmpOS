using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OmpOS.Harness.Acp;
using OmpOS.AgentWorker.Configuration;
using OmpOS.AgentWorker.Security;

namespace OmpOS.AgentWorker.Bridge;

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
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _attached = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="OmpProcessHost"/> class.</summary>
    /// <param name="options">The validated worker options.</param>
    /// <param name="redactor">Scrubs secrets from the child's stderr lines.</param>
    /// <param name="loggerFactory">Creates the loggers of the transport and the attachments.</param>
    public OmpProcessHost(IOptions<WorkerOptions> options, WorkerSecretRedactor redactor, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options.Value;
        _redactor = redactor;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Gets how many sessions are attached at this moment.</summary>
    public int AttachedCount => _attached.Count;

    /// <summary>Claims a session subject and prepares its omp child. The child is not started until <see cref="OmpAttachment.StartAsync"/>.</summary>
    /// <param name="sessionId">The validated session subject.</param>
    /// <param name="bearerToken">The connection's bearer token, redacted from the child's stderr lines while attached.</param>
    /// <returns>The attachment, or <see langword="null"/> when the session already has an attached connection.</returns>
    public OmpAttachment? TryAttach(string sessionId, string bearerToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(bearerToken);
        if (!_attached.TryAdd(sessionId, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)))
        {
            return null;
        }

        var workingDirectory = Path.Combine(_options.WorkspaceRoot, sessionId);
        var transport = new AcpStdioTransport(
            _options.Omp.Path,
            workingDirectory,
            _options.Omp.ExtraArguments,
            new Dictionary<string, string>(StringComparer.Ordinal),
            _loggerFactory.CreateLogger<AcpStdioTransport>());
        return new OmpAttachment(this, sessionId, workingDirectory, transport, _redactor.Track(bearerToken), _redactor, _loggerFactory.CreateLogger<OmpAttachment>());
    }

    /// <summary>Frees a session subject so a new connection may attach.</summary>
    /// <param name="sessionId">The session subject.</param>
    internal void Release(string sessionId)
    {
        if (_attached.TryRemove(sessionId, out var claim))
        {
            claim.TrySetResult();
        }
    }

    /// <summary>Returns a task that completes when the session's current claim, if any, is released: its omp child is gone and the subject is free again.</summary>
    /// <param name="sessionId">The session subject.</param>
    /// <returns>A task that is already complete when the session is not attached.</returns>
    public Task WaitForReleaseAsync(string sessionId) =>
        _attached.TryGetValue(sessionId, out var claim) ? claim.Task : Task.CompletedTask;
}
