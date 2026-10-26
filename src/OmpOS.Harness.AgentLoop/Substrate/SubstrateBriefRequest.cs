namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>The body of a <c>POST /brief</c> request. Every member is optional on the wire.</summary>
public sealed record SubstrateBriefRequest
{
    /// <summary>Gets the repository as <c>owner/name</c>, or null.</summary>
    public string? Repo { get; init; }

    /// <summary>Gets the branch, or null.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the Graph identifier the brief is about, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the surface the brief is read as. It must equal the surface of the bearer token.</summary>
    public string Surface { get; init; } = "omp";

    /// <summary>Gets the brief profile, <c>full</c> or <c>compact</c>, or null for the server default.</summary>
    public string? Profile { get; init; }
}
