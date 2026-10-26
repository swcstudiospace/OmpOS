namespace OmpOS.Harness.Herdr.Tests;

/// <summary>
/// One call made to <see cref="FakeHerdrSessionClient"/>, with its outcome: the resulting state name on success, or the
/// name of the failure type.
/// </summary>
/// <param name="Operation">The contract member that was called.</param>
/// <param name="SessionId">The session the call addressed, or null for a call that addresses none.</param>
/// <param name="Outcome">The resulting state on success, or the failure type name.</param>
internal sealed record HerdrTranscriptEntry(string Operation, string? SessionId, string Outcome);
