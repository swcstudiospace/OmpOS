namespace Autonogrammer.Api.Cloud;

/// <summary>A token for a git host and when it stops working. It is held in memory, sent in an <c>Authorization</c> header and, for a worker, in an environment variable; it is never put in a command line, a file or a log.</summary>
/// <param name="Token">The token. Never printed.</param>
/// <param name="ExpiresAt">When the host stops accepting it. <see cref="DateTimeOffset.MaxValue"/> for a token that does not expire.</param>
internal sealed record GitCredential(string Token, DateTimeOffset ExpiresAt)
{
    /// <inheritdoc />
    public override string ToString() => "GitCredential (token not shown)";
}
