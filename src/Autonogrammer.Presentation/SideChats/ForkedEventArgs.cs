namespace Autonogrammer.Presentation.SideChats;

/// <summary>Tells that a session was forked.</summary>
public sealed class ForkedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="sourceSessionId">The session that was forked.</param>
    /// <param name="result">The new session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceSessionId"/> or <paramref name="result"/> is null.</exception>
    public ForkedEventArgs(string sourceSessionId, ForkResult result)
    {
        ArgumentNullException.ThrowIfNull(sourceSessionId);
        ArgumentNullException.ThrowIfNull(result);
        SourceSessionId = sourceSessionId;
        Result = result;
    }

    /// <summary>Gets the session that was forked.</summary>
    public string SourceSessionId { get; }

    /// <summary>Gets the new session.</summary>
    public ForkResult Result { get; }
}
