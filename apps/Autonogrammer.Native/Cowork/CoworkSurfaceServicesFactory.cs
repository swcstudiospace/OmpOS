using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Threading;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Supplies the Cowork window services: the conversation delegate, the project list the rail's picker reads, and the new-task action.
/// The rail's task list reads the session host's own registry, the one that creates the tasks, so a created task appears at once.
/// The project list reads the active account's projects after a sync. Sign-out and an account switch clear the list and the selection.
/// </summary>
internal static class CoworkSurfaceServicesFactory
{
    /// <summary>Builds the window services and adds <see cref="CoworkSurfaceServices.OpenConversation"/>, the project list and the new-task action.</summary>
    /// <param name="head">The composed head.</param>
    /// <returns>The services, or null when Cowork is not available.</returns>
    public static CoworkSurfaceServices? TryBuild(AgentHead head)
    {
        var built = CoworkSessionCatalogAdapter.TryBuild(head);
        if (built is null)
        {
            return null;
        }

        var host = head.Cowork?.Sessions;
        if (host is null)
        {
            head.Services?.Artifacts.RegisterPanel(built.Shell);
            return built with
            {
                OpenConversation = sessionId => head.Cowork?.Sessions.ConversationOf(sessionId),
            };
        }

        var catalog = new HostTaskCatalog(host);
        var shell = new CoworkShellModel(catalog);
        built.Shell.Dispose();
        head.Services?.Artifacts.RegisterPanel(shell);
        var account = head.Cowork?.AccountContext;
        var projects = account is null ? null : new ProjectListModel(new AccountProjects(account));
        if (head.Services is { } services)
        {
            var gate = new NativeCoworkAccountGate(services.AccountHost.Session);
            services.AccountHost.Session.StateChanged += (_, _) => Dispatcher.UIThread.Post(() => ApplyAccountChange(catalog, projects, gate.IsSignedIn));
        }

        return built with
        {
            Catalog = catalog,
            Shell = shell,
            OpenConversation = sessionId => host.ConversationOf(sessionId),
            Projects = projects,
            CreateTask = async (projectId, cancellationToken) =>
            {
                var created = await host.CreateTaskAsync(projectId, cancellationToken).ConfigureAwait(true);
                catalog.NotifyChanged();
                return created;
            },
        };
    }

    // Runs on the UI thread after an account change. The rows of the previous account leave, and the selection with them.
    private static void ApplyAccountChange(HostTaskCatalog catalog, ProjectListModel? projects, bool signedIn)
    {
        catalog.NotifyChanged();
        if (projects is null)
        {
            return;
        }

        projects.Clear(signedIn ? null : CoworkDegradedSentences.SignedOut);
        if (signedIn)
        {
            _ = projects.LoadAsync(CancellationToken.None);
        }
    }

    /// <summary>The task list the rail reads: the host's registry, for the signed-in account only.</summary>
    private sealed class HostTaskCatalog(CoworkSessionHost host) : ICoworkSessionCatalog
    {
        /// <inheritdoc />
        public event EventHandler? Changed;

        /// <summary>Raises <see cref="Changed"/> after a task is created, or the account changes.</summary>
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc />
        public IReadOnlyList<CoworkSessionSummary> Snapshot() => host.TaskSummaries();
    }

    /// <summary>
    /// The projects of the signed-in account: its local copy after a sync. Signed out, or after the account changed during the read, the list is refused.
    /// The rail shows only the name and the archived flag, so the memory count is zero here; the editor's own read carries the counts a delete shows.
    /// </summary>
    private sealed class AccountProjects(CoworkAccountContext account) : IProjectListSource
    {
        /// <inheritdoc />
        public async Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken)
        {
            var read = await account.ListProjectsAsync(cancellationToken).ConfigureAwait(true);
            if (read.AccountKey is null)
            {
                return new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(false, false, null, read.SyncState, null);
            }

            // A project's metadata is one row, which is what a hard delete removes; the rail does not show the memory count.
            var rows = read.Projects
                .Select(project => new ProjectListItem(project.Id, project.Name, project.ArchivedAt is not null, MetadataEntries: 1, ProjectMemoryEntries: 0))
                .ToList();
            return new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, rows, read.SyncState, null);
        }
    }
}
