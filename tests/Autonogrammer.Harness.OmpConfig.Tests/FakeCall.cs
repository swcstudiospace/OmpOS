namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>One recorded invocation of the fake omp.</summary>
/// <param name="Arguments">The arguments after the executable name, one per recorded line.</param>
/// <param name="Environment">The recorded <c>PI_CODING_AGENT_DIR=</c> and <c>HOME=</c> lines of the child environment.</param>
/// <param name="Stdin">What the invocation read from standard input, or null when the fake did not read it.</param>
internal sealed record FakeCall(IReadOnlyList<string> Arguments, IReadOnlyList<string> Environment, string? Stdin);
