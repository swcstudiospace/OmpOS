namespace Autonogrammer.Presentation.Kanban;

/// <summary>
/// What Substrate knows about the session behind one card. Every field is what Substrate reported; the board never fills one in.
/// A field that is null is not reported and the card shows it as unavailable.
/// </summary>
/// <param name="TaskTitle">The title of the task the session works on, or null when unknown.</param>
/// <param name="GraphId">The Graph identifier of the session's plan, or null when unknown.</param>
/// <param name="LessonsCount">The number of lessons recorded for the task, or null when unknown.</param>
/// <param name="LeaseHolder">The holder of the task lease, or null when Substrate did not report one.</param>
/// <param name="Available">False when Substrate could not be reached; every other field is then ignored.</param>
public sealed record KanbanCardDetails(string? TaskTitle, string? GraphId, int? LessonsCount, string? LeaseHolder, bool Available);
