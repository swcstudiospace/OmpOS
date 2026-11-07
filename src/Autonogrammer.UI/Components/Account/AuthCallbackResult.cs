namespace Autonogrammer.UI.Components.Account;

/// <summary>The outcome of one hand-off. The sentence comes from account copy or the fixed other-tab sentence.</summary>
/// <param name="Status">How the wait ended.</param>
/// <param name="Sentence">The sentence to show, or null when there is nothing to say.</param>
/// <param name="Code">The code that was posted, or null.</param>
/// <param name="ScrubbedBeforePost">True when the query was removed before any post.</param>
/// <param name="RememberedPath">The path to remember, with no callback query.</param>
public sealed record AuthCallbackResult(
    AuthCallbackStatus Status,
    string? Sentence,
    string? Code,
    bool ScrubbedBeforePost,
    string RememberedPath);
