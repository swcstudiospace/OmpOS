namespace OmpOS.Workspace.Conventions;

/// <summary>The value of one top-level frontmatter key: a scalar or a list of scalars. A nested map is not kept.</summary>
/// <param name="Text">The scalar text, or the items joined with commas for a list. It is empty for a key with no value.</param>
/// <param name="Items">The items of a list value; empty for a scalar.</param>
/// <param name="IsList">Whether the value was written as a list.</param>
public sealed record ConventionFieldValue(string Text, IReadOnlyList<string> Items, bool IsList);
