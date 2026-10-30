namespace Autonogrammer.Presentation.Kanban;

/// <summary>The active model and provider of one chat, as the head reports them. Neither is guessed by the board.</summary>
/// <param name="Model">The model identifier, or null when unknown.</param>
/// <param name="Provider">The provider identifier, or null when unknown.</param>
public sealed record KanbanModelInfo(string? Model, string? Provider);
