using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Presentation.Cloud;

/// <summary>One line of the cloud agent list, ready to draw: plain, capped texts and a glyph key. Rows are immutable snapshots.</summary>
public sealed record CloudAgentRow
{
    private const int MaxNameChars = 120;
    private const int MaxLineChars = 160;

    /// <summary>Gets the agent identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name. A blank name reads <see cref="CloudCopy.UntitledAgent"/>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the status.</summary>
    public required CloudAgentStatus Status { get; init; }

    /// <summary>Gets the status in words, for example <c>Idle</c>.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets the glyph key of the status, from <see cref="StatusIcon"/>.</summary>
    public required string IconKey { get; init; }

    /// <summary>Gets the repositories as <c>owner/repository</c> names joined by commas, followed by the branch when there is one.</summary>
    public required string DetailText { get; init; }

    /// <summary>Gets the relative time of the last activity, or <see cref="CloudCopy.NoActivity"/>.</summary>
    public required string LastActivityText { get; init; }

    /// <summary>Gets the accessible name: <c>{name}, cloud agent, {status}, {last activity}</c>.</summary>
    public required string AccessibleName { get; init; }

    /// <summary>Creates the row of a record.</summary>
    /// <param name="record">The agent.</param>
    /// <param name="now">The current time, for the relative texts.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public static CloudAgentRow From(CloudAgentRecord record, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);

        var name = PanelText.Plain(record.Name, MaxNameChars);
        if (name.Length == 0)
        {
            name = CloudCopy.UntitledAgent;
        }

        var statusText = CloudAgentStatusMap.TextOf(record.Status);
        var activity = record.LastActivityAt is { } at ? RelativeTimeText.Format(at, now) : CloudCopy.NoActivity;
        return new CloudAgentRow
        {
            Id = record.Id,
            Name = name,
            Status = record.Status,
            StatusText = statusText,
            IconKey = CloudAgentStatusMap.IconOf(record.Status),
            DetailText = PanelText.Plain(DetailOf(record), MaxLineChars),
            LastActivityText = activity,
            AccessibleName = CloudCopy.RowAccessibleName(name, statusText, activity),
        };
    }

    private static string DetailOf(CloudAgentRecord record)
    {
        var repositories = string.Join(", ", record.Repositories.Select(static repository => repository.DisplayName));
        if (repositories.Length == 0)
        {
            repositories = CloudCopy.NoRepositories;
        }

        return string.IsNullOrWhiteSpace(record.Branch) ? repositories : CloudCopy.RepositoriesOnBranch(repositories, record.Branch.Trim());
    }
}
