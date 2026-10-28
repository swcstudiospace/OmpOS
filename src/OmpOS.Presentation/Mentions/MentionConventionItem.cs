namespace OmpOS.Presentation.Mentions;

/// <summary>A rule or skill a mention can attach.</summary>
/// <param name="Name">The rule or skill name; it is the mention's key.</param>
/// <param name="Description">The one-line description from the file's front matter, or <see langword="null"/>.</param>
/// <param name="Scope">Where the file lives: <c>project</c> or <c>user</c>, or <see langword="null"/>.</param>
public sealed record MentionConventionItem(string Name, string? Description, string? Scope);
