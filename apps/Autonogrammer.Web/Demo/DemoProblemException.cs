namespace Autonogrammer.Web.Demo;

/// <summary>A problem the Demo API answers with, in the same RFC 9457 shape as the real API (<c>type</c> is <c>/problems/{code}</c>).</summary>
internal sealed class DemoProblemException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DemoProblemException"/> class.</summary>
    public DemoProblemException()
        : this(500, null, "Demo failure.", "The Demo handler failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DemoProblemException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public DemoProblemException(string message)
        : this(500, null, "Demo failure.", message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DemoProblemException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The cause.</param>
    public DemoProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
        Status = 500;
        Title = "Demo failure.";
        Detail = message;
    }

    /// <summary>Initializes a new instance of the <see cref="DemoProblemException"/> class.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The problem code, or null for none.</param>
    /// <param name="title">The problem title.</param>
    /// <param name="detail">The problem detail.</param>
    internal DemoProblemException(int status, string? code, string title, string detail)
        : base(detail)
    {
        Status = status;
        Code = code;
        Title = title;
        Detail = detail;
    }

    /// <summary>Gets the HTTP status.</summary>
    internal int Status { get; }

    /// <summary>Gets the problem code, or null.</summary>
    internal string? Code { get; }

    /// <summary>Gets the title.</summary>
    internal string Title { get; } = string.Empty;

    /// <summary>Gets the detail.</summary>
    internal string Detail { get; } = string.Empty;

    /// <summary>Builds the 404 of a session that does not exist.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The problem.</returns>
    internal static DemoProblemException SessionNotFound(string id) =>
        new(404, null, "Session not found.", $"No session has id '{id}'.");
}
