namespace OmpOS.Web.Services;

/// <summary>Carries the session whose tool approval the API answered.</summary>
internal sealed class ApprovalDecidedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ApprovalDecidedEventArgs"/> class.</summary>
    /// <param name="sessionId">The session the decision belongs to.</param>
    internal ApprovalDecidedEventArgs(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        SessionId = sessionId;
    }

    /// <summary>Gets the session the decision belongs to.</summary>
    internal string SessionId { get; }
}
