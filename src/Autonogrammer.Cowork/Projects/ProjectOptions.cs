namespace Autonogrammer.Cowork.Projects;

/// <summary>Options for <c>Cowork:Projects</c>. A disabled API answers 404 cowork_disabled in a later plan.</summary>
public sealed class ProjectOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Projects";

    /// <summary>Gets or sets whether project sync is enabled.</summary>
    public bool Enabled { get; set; }
}
