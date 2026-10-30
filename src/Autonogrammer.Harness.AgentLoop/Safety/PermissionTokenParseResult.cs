namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>The result of parsing one permission token: the token, or the reason the text is not one.</summary>
/// <param name="Token">The token, or null when the text is invalid.</param>
/// <param name="Error">The reason the text is invalid, or null when <paramref name="Token"/> is set.</param>
public sealed record PermissionTokenParseResult(PermissionToken? Token, string? Error)
{
    /// <summary>Gets a value indicating whether the text was a valid token.</summary>
    public bool IsValid => Token is not null;
}
