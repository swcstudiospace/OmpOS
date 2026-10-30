namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>The outcome of expanding the variable references in one string.</summary>
/// <param name="Value">The expanded text. A reference that could not be expanded stays in it verbatim: it is never replaced by an empty string.</param>
/// <param name="Unresolved">The references that could not be expanded, without the <c>${</c> and <c>}</c>, for example <c>env:TOKEN</c>. Names only, never values.</param>
/// <param name="Unknown">The <c>${...}</c> texts that are not a variable of this format. They are kept verbatim.</param>
/// <param name="UsedEnvironment">Whether any reference read the environment.</param>
internal sealed record McpInterpolationResult(
    string Value,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<string> Unknown,
    bool UsedEnvironment);
