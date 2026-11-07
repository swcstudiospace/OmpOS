namespace Autonogrammer.Contracts.Cowork;

/// <summary>
/// The summary a desktop sends with <c>PUT /cowork/sessions/{id}</c>. It holds only what a server may store:
/// title, status, times, project id and device id. It never carries messages, paths or trusted folders.
/// </summary>
public sealed record CoworkSessionSummary
{
    /// <summary>The longest title that is stored, in characters.</summary>
    public const int MaxTitleLength = 200;

    private readonly string _title = string.Empty;

    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the title, capped at <see cref="MaxTitleLength"/> characters.</summary>
    public required string Title
    {
        get => _title;
        init => _title = Cap(value);
    }

    /// <summary>Gets the Cowork lifecycle.</summary>
    public required CoworkSessionStatus Status { get; init; }

    /// <summary>Gets when the task was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the task was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the project identifier, or null when the task is not in a project.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the device identifier that owns the transcript, or null when it is unknown.</summary>
    public string? DeviceId { get; init; }

    private static string Cap(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var trimmed = title.Trim();
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }
}
