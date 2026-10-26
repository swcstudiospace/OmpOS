namespace OmpOS.ArchitectureTests;

/// <summary>
/// One suppression found in first-party source: a <c>#pragma warning disable</c>, a
/// <c>SuppressMessage</c> attribute or an <c>.editorconfig</c> severity override set to none.
/// </summary>
/// <param name="Path">The repository-relative path, with forward slashes.</param>
/// <param name="Line">The 1-based line of the suppression.</param>
/// <param name="Kind">Which suppression form this is: pragma, SuppressMessage or editorconfig.</param>
/// <param name="RuleId">The suppressed rule id, <c>?</c> when the attribute arguments could not be read, or <c>*</c> for a pragma that names no rule.</param>
internal sealed record SuppressionOccurrence(string Path, int Line, string Kind, string RuleId);
