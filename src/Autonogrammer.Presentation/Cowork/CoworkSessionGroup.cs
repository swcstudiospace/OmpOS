namespace Autonogrammer.Presentation.Cowork;

/// <summary>The rows of one project, in catalog order.</summary>
public sealed class CoworkSessionGroup
{
    /// <summary>Creates a group.</summary>
    /// <param name="projectId">The project identifier, or null for tasks that are not in a project.</param>
    /// <param name="heading">The heading the rail shows.</param>
    /// <param name="rows">The rows, in catalog order.</param>
    public CoworkSessionGroup(string? projectId, string heading, IReadOnlyList<CoworkSessionRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentNullException.ThrowIfNull(rows);
        ProjectId = projectId;
        Heading = heading;
        Rows = rows;
    }

    /// <summary>Gets the project identifier, or null.</summary>
    public string? ProjectId { get; }

    /// <summary>Gets the heading.</summary>
    public string Heading { get; }

    /// <summary>Gets the rows.</summary>
    public IReadOnlyList<CoworkSessionRow> Rows { get; }
}
