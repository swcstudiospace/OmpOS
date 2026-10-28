namespace OmpOS.Web.Services;

/// <summary>The members of an RFC 9457 problem response the web head reads. Unknown members are ignored.</summary>
internal sealed record ApiProblemBody
{
    /// <summary>Gets the problem type, for example <c>/problems/session_closed</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Gets the short title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the HTTP status the body repeats.</summary>
    public int? Status { get; init; }

    /// <summary>Gets the human-readable detail.</summary>
    public string? Detail { get; init; }
}
