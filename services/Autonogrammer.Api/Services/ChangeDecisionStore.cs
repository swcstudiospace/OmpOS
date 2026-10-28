using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Contracts.Controls;

namespace Autonogrammer.Api.Services;

/// <summary>
/// The review decision of each changed file, kept for the life of the process. A decision belongs to the text it was made on: when a later
/// edit changes the file's text, the file is pending again.
/// </summary>
internal sealed class ChangeDecisionStore
{
    private readonly ConcurrentDictionary<(string SessionId, string Path), Decision> _decisions = new();

    /// <summary>Records a decision for the current text of a file.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="path">The path as the change list reports it.</param>
    /// <param name="status">The decision: accepted or rejected.</param>
    /// <param name="newText">The text after the latest edit, which the decision applies to.</param>
    public void Record(string sessionId, string path, ChangeReviewStatus status, string newText)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(newText);
        _decisions[(sessionId, path)] = new Decision(status, Fingerprint(newText));
    }

    /// <summary>Gets the review state of a file as it stands.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="path">The path as the change list reports it.</param>
    /// <param name="newText">The text after the latest edit.</param>
    /// <returns>The recorded decision when it was made on this text; otherwise pending.</returns>
    public ChangeReviewStatus StatusOf(string sessionId, string path, string newText)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(newText);
        return _decisions.TryGetValue((sessionId, path), out var decision) && string.Equals(decision.Fingerprint, Fingerprint(newText), StringComparison.Ordinal)
            ? decision.Status
            : ChangeReviewStatus.Pending;
    }

    private static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record Decision(ChangeReviewStatus Status, string Fingerprint);
}
