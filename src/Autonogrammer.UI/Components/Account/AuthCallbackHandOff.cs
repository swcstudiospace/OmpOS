using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;

namespace Autonogrammer.UI.Components.Account;

/// <summary>
/// The three-second acknowledgement rule and the wrong-tab rule, in C# so a test can run them against a fake channel. The boot script does the
/// same work before the router renders; this type is the seam the asking tab and the tests share.
/// </summary>
public static class AuthCallbackHandOff
{

    /// <summary>The channel name. It matches the boot script.</summary>
    public const string ChannelName = "autonogrammer-auth";

    /// <summary>How long the returning tab waits for an acknowledgement.</summary>
    public static readonly TimeSpan AcknowledgementWait = TimeSpan.FromSeconds(3);

    /// <summary>The sentence the returning tab shows once the asking tab has acknowledged. It is not an error.</summary>
    public const string SignedInOtherTabSentence = "You are signed in in your other tab.";

    /// <summary>
    /// The returning tab scrubs the callback query, posts the code, and waits for an acknowledgement. No acknowledgement is
    /// <see cref="AuthErrorKind"/> <c>LinkOpenedInWrongTab</c>. A channel that is not same-origin is refused and nothing is posted.
    /// </summary>
    /// <param name="relativePath">The address relative to the base, including any query.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="time">The clock the wait uses.</param>
    /// <param name="cancellationToken">Cancels the wait. The query is already scrubbed from the remembered path.</param>
    /// <returns>The outcome, including the path to remember.</returns>
    public static async Task<AuthCallbackResult> HandOffAsync(
        string relativePath,
        IAuthChannel channel,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(time);
        var remembered = RememberedPath(relativePath);
        var query = ReadQuery(relativePath);
        if (query.Code is null && query.Error is null && query.ErrorCode is null)
        {
            return new AuthCallbackResult(AuthCallbackStatus.None, null, null, false, remembered);
        }

        if (!channel.SameOrigin)
        {
            return new AuthCallbackResult(AuthCallbackStatus.Error, AccountCopy.For(AuthErrorKind.LinkOpenedInWrongTab), null, true, remembered);
        }

        if (query.Code is null)
        {
            return new AuthCallbackResult(AuthCallbackStatus.Error, AccountCopy.For(AuthErrorKind.LinkOpenedInWrongTab), null, true, remembered);
        }

        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = channel.Subscribe(envelope =>
        {
            if (string.Equals(envelope.Kind, "ack", StringComparison.Ordinal))
            {
                acknowledged.TrySetResult();
            }
        });

        channel.Post(new AuthChannelEnvelope("code", query.Code));
        using var timeout = new CancellationTokenSource(AcknowledgementWait, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await acknowledged.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AuthCallbackResult(
                AuthCallbackStatus.Unacknowledged,
                AccountCopy.For(AuthErrorKind.LinkOpenedInWrongTab),
                query.Code,
                true,
                remembered);
        }

        return new AuthCallbackResult(AuthCallbackStatus.Acknowledged, SignedInOtherTabSentence, query.Code, true, remembered);
    }

    /// <summary>
    /// The asking tab listens, acknowledges a code, then exchanges it. The exchange runs only after the acknowledgement is posted.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="exchange">Exchanges the code. It is not called when the channel is refused.</param>
    /// <returns>A subscription. Disposing it stops listening.</returns>
    public static IDisposable ListenForExchange(IAuthChannel channel, Func<string, Task> exchange)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(exchange);
        if (!channel.SameOrigin)
        {
            return new Noop();
        }

        return channel.Subscribe(envelope =>
        {
            if (!string.Equals(envelope.Kind, "code", StringComparison.Ordinal) || string.IsNullOrEmpty(envelope.Code))
            {
                return;
            }

            channel.Post(new AuthChannelEnvelope("ack", null));
            _ = exchange(envelope.Code);
        });
    }

    /// <summary>Drops a sign-in callback query so a remembered return path never contains <c>code</c> or <c>error</c>.</summary>
    /// <param name="relativePath">The path, possibly with a query.</param>
    /// <returns>The path without those parameters.</returns>
    public static string RememberedPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var trimmed = relativePath.Trim();
        var queryAt = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (queryAt < 0)
        {
            return trimmed;
        }

        var path = trimmed[..queryAt];
        var query = trimmed[(queryAt + 1)..];
        var hashAt = query.IndexOf('#', StringComparison.Ordinal);
        var fragment = hashAt >= 0 ? query[hashAt..] : string.Empty;
        if (hashAt >= 0)
        {
            query = query[..hashAt];
        }

        var kept = new List<string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Split('=', 2)[0];
            if (IsCallbackParameter(name))
            {
                continue;
            }

            kept.Add(part);
        }

        if (kept.Count == 0)
        {
            return path + fragment;
        }

        return path + "?" + string.Join('&', kept) + fragment;
    }

    /// <summary>Accepts a typed email code of 6 to 10 digits after spaces and hyphens are removed.</summary>
    /// <param name="code">The code as typed.</param>
    /// <returns>The digits, or null when the text is not a code of that length.</returns>
    public static string? NormalizeEmailCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        var digits = new char[code.Length];
        var count = 0;
        foreach (var character in code)
        {
            if (character is ' ' or '-')
            {
                continue;
            }

            if (character is < '0' or > '9')
            {
                return null;
            }

            digits[count++] = character;
        }

        return count is >= 6 and <= 10 ? new string(digits, 0, count) : null;
    }

    private static bool IsCallbackParameter(string name) =>
        string.Equals(name, "code", StringComparison.Ordinal)
        || string.Equals(name, "error", StringComparison.Ordinal)
        || string.Equals(name, "error_code", StringComparison.Ordinal)
        || string.Equals(name, "error_description", StringComparison.Ordinal);

    private static (string? Code, string? Error, string? ErrorCode) ReadQuery(string relativePath)
    {
        var queryAt = relativePath.IndexOf('?', StringComparison.Ordinal);
        if (queryAt < 0)
        {
            return (null, null, null);
        }

        var query = relativePath[(queryAt + 1)..];
        var hashAt = query.IndexOf('#', StringComparison.Ordinal);
        if (hashAt >= 0)
        {
            query = query[..hashAt];
        }

        string? code = null;
        string? error = null;
        string? errorCode = null;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var name = Uri.UnescapeDataString(pieces[0]);
            var value = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;
            if (string.Equals(name, "code", StringComparison.Ordinal))
            {
                code = value;
            }
            else if (string.Equals(name, "error", StringComparison.Ordinal))
            {
                error = value;
            }
            else if (string.Equals(name, "error_code", StringComparison.Ordinal))
            {
                errorCode = value;
            }
        }

        return (string.IsNullOrEmpty(code) ? null : code, string.IsNullOrEmpty(error) ? null : error, string.IsNullOrEmpty(errorCode) ? null : errorCode);
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
