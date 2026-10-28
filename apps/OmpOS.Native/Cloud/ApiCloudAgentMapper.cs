using OmpOS.Contracts.Cloud;
using OmpOS.Presentation.Cloud;

namespace OmpOS.Native.Cloud;

/// <summary>
/// Maps the control plane's wire records (the <c>OmpOS.Contracts.Cloud</c> namespace) to the records the Cloud agents page shows
/// (the <c>OmpOS.Presentation.Cloud</c> namespace). Nothing here reads a secret, a worker address or a token: the wire record has none, and the worker
/// reference is dropped on purpose.
/// </summary>
internal static class ApiCloudAgentMapper
{
    private const int MaxDetailChars = 300;

    /// <summary>Maps one agent.</summary>
    /// <param name="wire">The control plane's agent.</param>
    /// <returns>The agent as the page shows it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="wire"/> is null.</exception>
    public static Presentation.Cloud.CloudAgentRecord ToRecord(CloudAgentResponse wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        var repositories = wire.Repositories ?? [];
        return new Presentation.Cloud.CloudAgentRecord
        {
            Id = wire.Id,
            Name = wire.Name,
            Status = ToStatus(wire.Status),
            CreatedAt = wire.CreatedAt,
            Repositories = [.. repositories.Select(static repository => new Presentation.Cloud.CloudRepository(repository.Address))],
            Branch = string.IsNullOrWhiteSpace(wire.Branch) ? null : wire.Branch,
            BaseBranch = repositories.Count > 0 && !string.IsNullOrWhiteSpace(repositories[0].BaseBranch) ? repositories[0].BaseBranch : null,
            Model = string.IsNullOrWhiteSpace(wire.Model) ? null : wire.Model,
            IdleMinutes = wire.IdleMinutes,
            LastActivityAt = wire.LastActivityAt,
            SessionId = string.IsNullOrWhiteSpace(wire.SessionId) ? null : wire.SessionId,
            StatusDetail = wire.Error is { } error ? Cap(error.Message) : null,
            PullRequests = [.. (wire.PullRequests ?? []).Select(static pullRequest => ToLink(pullRequest)).OfType<CloudPullRequestLink>()],
            Timeline = Timeline(wire),
        };
    }

    /// <summary>Maps a pull request. A pull request whose address is not an absolute https address has no link.</summary>
    /// <param name="wire">The control plane's pull request.</param>
    /// <param name="title">The title the user gave it, or null when the control plane does not report one.</param>
    /// <returns>The link, or null when the address cannot be opened safely.</returns>
    public static CloudPullRequestLink? ToLink(CloudPullRequest wire, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return wire.Url.IsAbsoluteUri && string.Equals(wire.Url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            ? new CloudPullRequestLink(wire.Repository, wire.Url, title)
            : null;
    }

    /// <summary>Maps a status, one to one.</summary>
    /// <param name="status">The wire status.</param>
    /// <returns>The presentation status.</returns>
    public static Presentation.Cloud.CloudAgentStatus ToStatus(Contracts.Cloud.CloudAgentStatus status) => status switch
    {
        Contracts.Cloud.CloudAgentStatus.Provisioning => Presentation.Cloud.CloudAgentStatus.Provisioning,
        Contracts.Cloud.CloudAgentStatus.Starting => Presentation.Cloud.CloudAgentStatus.Starting,
        Contracts.Cloud.CloudAgentStatus.Running => Presentation.Cloud.CloudAgentStatus.Running,
        Contracts.Cloud.CloudAgentStatus.Idle => Presentation.Cloud.CloudAgentStatus.Idle,
        Contracts.Cloud.CloudAgentStatus.Stopping => Presentation.Cloud.CloudAgentStatus.Stopping,
        Contracts.Cloud.CloudAgentStatus.Stopped => Presentation.Cloud.CloudAgentStatus.Stopped,
        Contracts.Cloud.CloudAgentStatus.Failed => Presentation.Cloud.CloudAgentStatus.Failed,
        Contracts.Cloud.CloudAgentStatus.Deleting => Presentation.Cloud.CloudAgentStatus.Deleting,
        Contracts.Cloud.CloudAgentStatus.Orphaned => Presentation.Cloud.CloudAgentStatus.Orphaned,
        Contracts.Cloud.CloudAgentStatus.LimitReached => Presentation.Cloud.CloudAgentStatus.LimitReached,
        _ => Presentation.Cloud.CloudAgentStatus.Failed,
    };

    /// <summary>
    /// Builds the history the page can show from what the control plane reports: the creation and the current status. The control plane keeps no
    /// status history yet, so this is two rows at most.
    /// </summary>
    private static List<CloudStatusEvent> Timeline(CloudAgentResponse wire)
    {
        var events = new List<CloudStatusEvent> { new(wire.CreatedAt, Presentation.Cloud.CloudAgentStatus.Provisioning) };
        var current = ToStatus(wire.Status);
        if (current != Presentation.Cloud.CloudAgentStatus.Provisioning)
        {
            events.Add(new CloudStatusEvent(wire.LastActivityAt, current, wire.Error is { } error ? Cap(error.Message) : null));
        }

        return events;
    }

    private static string Cap(string text) => text.Length <= MaxDetailChars ? text : text[..MaxDetailChars];
}
