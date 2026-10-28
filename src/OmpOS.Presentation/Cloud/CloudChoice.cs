namespace OmpOS.Presentation.Cloud;

/// <summary>One option of a pick list the host offers: an environment or a model.</summary>
/// <param name="Id">The identifier the host expects back in <see cref="CloudAgentRequest"/>.</param>
/// <param name="Name">The name the user reads.</param>
public sealed record CloudChoice(string Id, string Name);
