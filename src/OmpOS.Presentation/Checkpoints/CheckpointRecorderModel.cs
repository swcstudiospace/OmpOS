using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Checkpoints;

/// <summary>
/// Feeds the checkpoint store from the transcript, so checkpoints are recorded without the user doing anything. The head calls <see cref="UpdateAsync"/> wherever it calls
/// <c>ChangeSetModel.Update</c> (it takes the same blocks and the same workspace root). Each turn that changed files is recorded; a turn whose edits did not change since the last call
/// is skipped, so a streaming transcript costs one write per real change. A backend that fails never fails the caller: <see cref="LastError"/> says what went wrong and the next update tries again.
/// </summary>
/// <remarks>Calls are serialised, so two updates never record at once.</remarks>
public sealed class CheckpointRecorderModel : ObservableModel, IDisposable
{
    private readonly string _sessionId;
    private readonly string _rootPath;
    private readonly ICheckpointBackend _backend;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string> _recorded = new(StringComparer.Ordinal);
    private string _lastError = string.Empty;
    private int _recordedCount;

    /// <summary>Creates the recorder of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="rootPath">The absolute path of the workspace root the agent's paths are relative to.</param>
    /// <param name="backend">Keeps the checkpoints.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> or <paramref name="rootPath"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> is null.</exception>
    public CheckpointRecorderModel(string sessionId, string rootPath, ICheckpointBackend backend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(backend);

        _sessionId = sessionId;
        _rootPath = rootPath;
        _backend = backend;
    }

    /// <summary>Gets the sentence for the last failed recording, or an empty string.</summary>
    public string LastError => _lastError;

    /// <summary>Gets how many times a turn was written to the backend.</summary>
    public int RecordedCount => _recordedCount;

    /// <summary>Releases the lock that serialises updates.</summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>Records the turns of the transcript that changed files since the last call.</summary>
    /// <param name="blocks">The transcript's blocks, in order.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>A task that completes when every changed turn was handed to the backend.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> is null.</exception>
    public async Task UpdateAsync(IReadOnlyList<UiBlock> blocks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var turns = TurnChangeSet.Build(blocks, _rootPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            foreach (var turn in turns)
            {
                var signature = Signature(turn);
                if (_recorded.TryGetValue(turn.TurnKey, out var known) && string.Equals(known, signature, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    await _backend.RecordTurnAsync(_sessionId, turn, cancellationToken).ConfigureAwait(true);
                    _recorded[turn.TurnKey] = signature;
                    _recordedCount++;
                    SetProperty(ref _lastError, string.Empty, nameof(LastError));
                    NotifyPropertyChanged(nameof(RecordedCount));
                }
                catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                {
                    SetProperty(ref _lastError, CheckpointsCopy.Failed(exception.Message), nameof(LastError));
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Signature(CheckpointTurnEdits turn)
    {
        var builder = new StringBuilder();
        foreach (var file in turn.Files)
        {
            builder.Append(file.Path).Append('\0').Append(file.OldText is null ? "-" : file.OldText.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(file.NewText.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(file.NewText)))).Append('\n');
        }

        return builder.ToString();
    }
}
