using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Projects;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>
/// The Cowork seams a window needs. Every member is a contract or a presentation model. This type names no Cowork assembly type.
/// A window built without it shows Code only and no switch.
/// </summary>
public sealed record CoworkSurfaceServices
{
    /// <summary>Gets the Cowork catalog the rail and the activity chip read.</summary>
    public required ICoworkSessionCatalog Catalog { get; init; }

    /// <summary>Gets the Cowork layout. The host does not replace it on a switch.</summary>
    public required CoworkShellModel Shell { get; init; }

    /// <summary>Gets the stored surface text, or null when the file has none. The host restores it only while Cowork is available.</summary>
    public string? SavedSurface { get; init; }

    /// <summary>Gets the writer of a surface change, or null when the choice is not stored.</summary>
    public Action<SessionSurface>? PersistSurface { get; init; }

    /// <summary>Gets the reader of Code rows for the hidden-surface chip, or null for an empty list.</summary>
    public Func<IReadOnlyList<AgentRow>>? ReadCodeRows { get; init; }

    /// <summary>Gets the subscription for Code row changes, or null when there is no signal.</summary>
    public Action<EventHandler>? SubscribeCode { get; init; }

    /// <summary>Gets the matching removal, or null when there is no signal.</summary>
    public Action<EventHandler>? UnsubscribeCode { get; init; }

    /// <summary>Gets the conversation model for a selected session, or null for the empty state. The value is a presentation model.</summary>
    public Func<string, object?>? OpenConversation { get; init; }

    /// <summary>Gets the project list the rail's project picker reads. It is bound to the signed-in account, or null when there is no account.</summary>
    public ProjectListModel? Projects { get; init; }

    /// <summary>
    /// Gets the new-task action, or null when Cowork cannot create tasks. The argument is the project the task is linked to, or null for an unlinked task.
    /// The result is the new task's identifier and a notice to show, or null.
    /// </summary>
    public Func<string?, CancellationToken, Task<(string Id, string? Notice)>>? CreateTask { get; init; }
}
