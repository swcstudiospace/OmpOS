namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The machine answered, but the probe's answer could not be read: a line was missing, malformed or unknown, the output was cut, or the probe
/// exited with an error. The answer is never guessed; nothing is written.
/// </summary>
/// <param name="Reason">What was wrong with the answer, in one plain sentence.</param>
/// <param name="Excerpt">The first characters of the answer or the error, capped by <see cref="RemoteOutputExcerpt"/>, or null.</param>
public sealed record ProvisionProbeFailed(string Reason, string? Excerpt) : RemoteProvisionResult;
