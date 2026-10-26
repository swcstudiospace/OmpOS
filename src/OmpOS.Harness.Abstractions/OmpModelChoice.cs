namespace OmpOS.Harness.Abstractions;

/// <summary>One model the engine offers for a session.</summary>
/// <param name="Value">The engine's identifier of the model, <c>provider/id</c> for omp. Sent back verbatim to select it.</param>
/// <param name="Name">The display name the engine gave the model.</param>
public sealed record OmpModelChoice(string Value, string Name);
