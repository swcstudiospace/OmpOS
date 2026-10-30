namespace Autonogrammer.Api.Cloud;

/// <summary>A refusal the control plane reports as an RFC 9457 problem. The detail is written by Autonogrammer and never repeats a prompt, a secret or a provider response.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Code">The stable machine-readable code, put in the problem <c>type</c> as <c>/problems/{code}</c>.</param>
/// <param name="Title">The problem title.</param>
/// <param name="Detail">The problem detail.</param>
internal sealed record CloudProblem(int Status, string Code, string Title, string Detail)
{
    /// <summary>Gets the caller's agent that does not exist or belongs to another owner. Both read the same.</summary>
    /// <param name="id">The identifier the caller named.</param>
    /// <returns>The problem.</returns>
    public static CloudProblem AgentNotFound(string id) => new(404, "agent_not_found", "Cloud agent not found.", $"No cloud agent has id '{id}'.");

    /// <summary>Builds a 409 problem.</summary>
    /// <param name="code">The stable code.</param>
    /// <param name="title">The title.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>The problem.</returns>
    public static CloudProblem Conflict(string code, string title, string detail) => new(409, code, title, detail);

    /// <summary>Builds a 400 problem.</summary>
    /// <param name="code">The stable code.</param>
    /// <param name="title">The title.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>The problem.</returns>
    public static CloudProblem BadRequest(string code, string title, string detail) => new(400, code, title, detail);
}
