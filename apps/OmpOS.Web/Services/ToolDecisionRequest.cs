namespace OmpOS.Web.Services;

/// <summary>The body sent to <c>POST /sessions/{id}/tool-approvals/{requestId}</c>: the only member is <c>decision</c>, <c>allow</c> or <c>deny</c>.</summary>
internal sealed record ToolDecisionRequest
{
    /// <summary>Gets the decision: <c>allow</c> or <c>deny</c>.</summary>
    public required string Decision { get; init; }
}
