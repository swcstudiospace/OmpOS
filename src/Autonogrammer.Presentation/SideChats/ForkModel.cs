using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.SideChats;

/// <summary>
/// Fork and duplicate of one session (<c>/fork</c>, <c>/branch</c>, <c>/duplicate</c>): a new session that continues from the same history, made through <see cref="ISessionForker"/>. A fork from a message keeps
/// the conversation up to that message; a duplicate keeps all of it. The model opens the new session by raising <see cref="Forked"/>, so the head decides where it shows; the original session is not changed.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class ForkModel : ObservableModel
{
    private readonly string _sessionId;
    private readonly ISessionForker _forker;
    private bool _isBusy;
    private bool _hasError;
    private string _statusText = string.Empty;

    /// <summary>Creates the fork model of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="forker">Makes the forks.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="forker"/> is null.</exception>
    public ForkModel(string sessionId, ISessionForker forker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(forker);
        _sessionId = sessionId;
        _forker = forker;
    }

    /// <summary>Occurs after a fork made a new session. The head opens it.</summary>
    public event EventHandler<ForkedEventArgs>? Forked;

    /// <summary>Gets whether a fork is running.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets the last sentence for the status line and assistive technology.</summary>
    public string StatusText => _statusText;

    /// <summary>Gets whether <see cref="StatusText"/> reports a failure or a refusal.</summary>
    public bool HasError => _hasError;

    /// <summary>Gets whether the engine can fork from a message.</summary>
    public bool CanForkFromMessage => _forker.CanForkFromMessage;

    /// <summary>Forks the whole conversation, like <c>/fork</c>, <c>/branch</c> and <c>/duplicate</c>.</summary>
    /// <param name="cancellationToken">Cancels the fork.</param>
    /// <returns>The new session, or <see langword="null"/> when the fork failed (the status says why).</returns>
    public Task<ForkResult?> DuplicateAsync(CancellationToken cancellationToken) => ForkAsync(null, cancellationToken);

    /// <summary>Forks the conversation up to a message and drops the later ones.</summary>
    /// <param name="messageCount">The number of messages to keep, 1 or more.</param>
    /// <param name="cancellationToken">Cancels the fork.</param>
    /// <returns>The new session, or <see langword="null"/> when the engine cannot fork from a message, the count is out of range, or the fork failed (the status says why).</returns>
    public Task<ForkResult?> ForkFromMessageAsync(int messageCount, CancellationToken cancellationToken)
    {
        if (messageCount < 1)
        {
            SetStatus(ForkCopy.CountOutOfRange, isError: true);
            return Task.FromResult<ForkResult?>(null);
        }

        if (!_forker.CanForkFromMessage)
        {
            SetStatus(ForkCopy.WholeChatOnly, isError: true);
            return Task.FromResult<ForkResult?>(null);
        }

        return ForkAsync(messageCount, cancellationToken);
    }

    private async Task<ForkResult?> ForkAsync(int? messageCount, CancellationToken cancellationToken)
    {
        if (_isBusy)
        {
            SetStatus(ForkCopy.Busy, isError: false);
            return null;
        }

        SetProperty(ref _isBusy, true, nameof(IsBusy));
        try
        {
            var result = await _forker.ForkAsync(new ForkRequest(_sessionId, messageCount), cancellationToken).ConfigureAwait(true);
            SetStatus(ForkCopy.Forked(result.Title.Length > 0 ? result.Title : result.NewSessionId, messageCount), isError: false);
            Forked?.Invoke(this, new ForkedEventArgs(_sessionId, result));
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus(ForkCopy.Failed(exception.Message), isError: true);
            return null;
        }
        finally
        {
            SetProperty(ref _isBusy, false, nameof(IsBusy));
        }
    }

    private void SetStatus(string text, bool isError)
    {
        SetProperty(ref _statusText, text, nameof(StatusText));
        SetProperty(ref _hasError, isError, nameof(HasError));
    }
}
