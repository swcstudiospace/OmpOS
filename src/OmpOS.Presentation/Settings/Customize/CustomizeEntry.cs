namespace OmpOS.Presentation.Settings.Customize;

/// <summary>One row of a read-only Customize catalogue.</summary>
/// <param name="Title">The name of the rule, skill or command.</param>
/// <param name="Scope">Where the entry comes from.</param>
/// <param name="Origin">The file path (relative to the workspace for project entries) or the origin of a built-in entry.</param>
/// <param name="Description">A short description; empty when the entry has none.</param>
public sealed record CustomizeEntry(string Title, CustomizeScope Scope, string Origin, string Description)
{
    /// <summary>Gets the words for <see cref="Scope"/>.</summary>
    public string ScopeLabel => CustomizeCopy.ScopeLabel(Scope);
}
