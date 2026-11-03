namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>One artifact call result.</summary>
/// <typeparam name="T">The payload.</typeparam>
/// <param name="State">What the call answered.</param>
/// <param name="Value">The payload when <see cref="State"/> is <see cref="ArtifactCallState.Ok"/>.</param>
/// <param name="RetryAfter">The delay a rate-limited answer asked for, or null.</param>
internal sealed record ArtifactCall<T>(ArtifactCallState State, T? Value, TimeSpan? RetryAfter);
