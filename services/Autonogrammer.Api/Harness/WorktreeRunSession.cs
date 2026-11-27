using System.Collections.Frozen;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Worktrees;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// The harness session of one run in its own git worktree. The worktree is created when the session is created and removed
/// when the session is disposed, so completion, failure and cancellation all remove it. The omp session is built only after
/// the worktree exists, because omp starts in that directory and reports it as its working directory.
/// </summary>
internal sealed class WorktreeRunSession : IOmpSession
{
    private static readonly IReadOnlyDictionary<string, string> NoCapabilities = FrozenDictionary<string, string>.Empty;

    private readonly WorktreeManager _worktrees;
    private readonly string _repository;
    private readonly string _runId;
    private readonly Func<string, IOmpSession> _openIn;
    private IOmpSession? _session;
    private bool _worktreeCreated;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="WorktreeRunSession"/> class.</summary>
    /// <param name="worktrees">Creates and removes the run's worktree.</param>
    /// <param name="repository">The repository the worktree is created from.</param>
    /// <param name="runId">The run identifier; it names the worktree directory.</param>
    /// <param name="openIn">Builds the omp session that runs in the given worktree directory.</param>
    public WorktreeRunSession(WorktreeManager worktrees, string repository, string runId, Func<string, IOmpSession> openIn)
    {
        _worktrees = worktrees;
        _repository = repository;
        _runId = runId;
        _openIn = openIn;
    }

    /// <inheritdoc />
    public HarnessState State => _session?.State ?? HarnessState.Disconnected;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities => _session?.AdvertisedCapabilities ?? NoCapabilities;

    /// <inheritdoc />
    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await session.CreateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await session.ResumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
        Connected().SubmitPromptAsync(prompt, options, cancellationToken);

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken) =>
        _session is null ? Task.CompletedTask : _session.CancelAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (_worktreeCreated)
            {
                await _worktrees.CloseAsync(_runId, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task<IOmpSession> OpenAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            var worktree = await _worktrees.CreateAsync(_repository, _runId, cancellationToken).ConfigureAwait(false);
            _worktreeCreated = true;
            _session = _openIn(worktree.Path);
        }

        return _session;
    }

    private IOmpSession Connected() =>
        _session ?? throw new InvalidOperationException("The run has no worktree yet; CreateAsync must run before a prompt is submitted.");
}
