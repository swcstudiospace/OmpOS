namespace Autonogrammer.Api.Cowork;

/// <summary>The outcome of storing one Cowork summary.</summary>
internal enum CoworkPutOutcome
{
    /// <summary>The summary was stored for the first time.</summary>
    Created,

    /// <summary>An existing summary of the same owner was replaced.</summary>
    Updated,

    /// <summary>The identifier belongs to another owner, to a Code session, or to a removed summary.</summary>
    NotFound,

    /// <summary>The owner already holds the configured number of Cowork summaries.</summary>
    Conflict,
}
