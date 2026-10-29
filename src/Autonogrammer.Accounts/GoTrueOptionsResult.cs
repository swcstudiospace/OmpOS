namespace Autonogrammer.Accounts;

/// <summary>
/// The outcome of <see cref="GoTrueOptions.Create(string?, string?)"/>: the options, or the name of the field that failed and a fixed reason. Neither
/// ever contains a value that was supplied.
/// </summary>
public sealed record GoTrueOptionsResult
{
    private GoTrueOptionsResult(GoTrueOptions? options, string? field, string? problem)
    {
        Options = options;
        Field = field;
        Problem = problem;
    }

    /// <summary>Gets the options, or null when validation failed.</summary>
    public GoTrueOptions? Options { get; }

    /// <summary>Gets the name of the field that failed, or null when validation passed.</summary>
    public string? Field { get; }

    /// <summary>Gets the fixed reason the field failed, or null when validation passed.</summary>
    public string? Problem { get; }

    /// <summary>Gets a value indicating whether validation passed.</summary>
    public bool IsValid => Options is not null;

    /// <summary>Gets a message naming the failed field, or null when validation passed.</summary>
    public string? Message => Field is null ? null : $"{Field} {Problem}";

    internal static GoTrueOptionsResult Valid(GoTrueOptions options) => new(options, null, null);

    internal static GoTrueOptionsResult Invalid(string field, string problem) => new(null, field, problem);
}
