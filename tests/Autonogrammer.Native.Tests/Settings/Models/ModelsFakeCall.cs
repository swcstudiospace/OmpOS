namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>One call the fake omp recorded: what it was asked and what it saw, never a key value except the standard input of an import.</summary>
/// <param name="Arguments">The arguments, one per entry.</param>
/// <param name="Environment">The <c>PI_CODING_AGENT_DIR</c> and <c>HOME</c> lines.</param>
/// <param name="KeyNames">The names of the provider key variables that were set.</param>
/// <param name="StandardInput">What was written to standard input, or null when nothing was read.</param>
internal sealed record ModelsFakeCall(
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> Environment,
    IReadOnlyList<string> KeyNames,
    string? StandardInput);
