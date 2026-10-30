using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Presentation.Checkpoints;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.Editor;
using Autonogrammer.Presentation.Modes;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Worktrees;
using Autonogrammer.Workspace.Sets;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>
/// The services the main view needs from the head, as seams the views library can see. The head's composition (<c>HeadServices</c> in the native app) maps its members onto this record: the views library cannot reference the
/// app. Every member is optional: a member left null leaves its feature out of the window (no environment picker, no cloud hand-off, no checkpoints timeline) and the commands that need it report that they are not available here.
/// </summary>
public sealed record ShellServices
{
    /// <summary>Gets the hooks service whose run log the run-log drawer shows, or null when hooks are not wired.</summary>
    public IHooksService? Hooks { get; init; }

    /// <summary>Gets the remote environments host whose profiles the environment picker lists, or null for a window with no remote sessions.</summary>
    public IRemoteEnvironmentsHost? RemoteEnvironments { get; init; }

    /// <summary>Gets the shared record of what the next new session starts on (environment, workspace set folders), or null when the head cannot start sessions elsewhere.</summary>
    public PendingSessionTarget? NewSessionTarget { get; init; }

    /// <summary>Gets the checkpoint backend the per-session Checkpoints timelines use, or null when checkpoints are off.</summary>
    public ICheckpointBackend? Checkpoints { get; init; }

    /// <summary>Gets the worktree backend the Worktrees panel uses, or null when worktrees are not wired.</summary>
    public IWorktreeBackend? Worktrees { get; init; }

    /// <summary>Gets the host that runs best-of-n candidates in their worktrees, or null when the head has none (the command then says so).</summary>
    public IBestOfNHost? BestOfN { get; init; }

    /// <summary>Gets the factory of the source of skills offered as custom modes, given the session's workspace root, or null for a window with no custom modes.</summary>
    public Func<string, IModeSkillSource>? CreateModeSkills { get; init; }

    /// <summary>Gets the launcher of the About window.</summary>
    public IAboutLauncher? About { get; init; }

    /// <summary>Gets the launcher that opens addresses in the system browser.</summary>
    public IUrlLauncher? UrlLauncher { get; init; }

    /// <summary>Gets the display switches (line numbers, status indicators and the others), or null for the defaults.</summary>
    public DisplaySettings? Display { get; init; }

    /// <summary>Gets the store of saved workspace sets, or null for a window with no saved sets.</summary>
    public WorkspaceSetStore? WorkspaceSets { get; init; }

    /// <summary>Gets the backend of the workspace set dialog (folder checks and <c>.code-workspace</c> import and export), or null for a window with no workspace dialog.</summary>
    public IWorkspaceSetPickerBackend? WorkspacePicker { get; init; }

    /// <summary>Gets the cloud agents host, or null when the API address is not configured (the cloud hand-off then says what is missing).</summary>
    public ICloudAgentsHost? Cloud { get; init; }
}
