using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>What the host tells <see cref="IHooksService.StartSessionAsync"/> about the conversation it starts hooks for.</summary>
public sealed record HooksSessionRequest
{
    /// <summary>
    /// Gets the Autonogrammer session identifier: a safe single path segment (see <see cref="Launch.SessionStateLayout"/>). It names the session's state directory
    /// and is the <c>conversation_id</c> hooks receive unless <see cref="ConversationId"/> is set.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the conversation identifier hooks receive as <c>conversation_id</c>, or null to use <see cref="SessionId"/>.</summary>
    public string? ConversationId { get; init; }

    /// <summary>Gets the absolute workspace roots, primary first.</summary>
    public required IReadOnlyList<string> WorkspaceRoots { get; init; }

    /// <summary>Gets a value indicating whether the session is remote or hosted.</summary>
    public bool IsRemote { get; init; }

    /// <summary>Gets the file source for the workspace, or null for the local file system.</summary>
    public IHookFileSource? Files { get; init; }

    /// <summary>Gets whether project hooks are trusted regardless of the user's trust list; null uses the list.</summary>
    public bool? TrustProjectHooks { get; init; }

    /// <summary>Gets the model slug hooks receive as <c>model</c>.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Gets the structured model identifier, or null.</summary>
    public string? ModelId { get; init; }

    /// <summary>Gets the signed-in user's email, or null.</summary>
    public string? UserEmail { get; init; }

    /// <summary>Gets the path of the conversation transcript, or null when the host does not know it.</summary>
    public string? TranscriptPath { get; init; }

    /// <summary>Gets a value indicating whether the session is a background (cloud) agent.</summary>
    public bool IsBackgroundAgent { get; init; }
}
