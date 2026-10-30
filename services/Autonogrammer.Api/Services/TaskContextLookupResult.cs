using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Api.Services;

/// <summary>The outcome of asking the task-context source about a session.</summary>
/// <param name="Context">The context, or null when no source answered.</param>
/// <param name="Reason">A plain sentence that says why there is no context, or null when there is one.</param>
internal sealed record TaskContextLookupResult(TaskContext? Context, string? Reason);
