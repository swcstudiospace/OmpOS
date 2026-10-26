namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// Everything needed to start omp. No shell is involved: the executable runs directly with the listed arguments.
/// </summary>
/// <param name="ExecutablePath">The absolute path of the executable.</param>
/// <param name="Arguments">The arguments, one element each, passed through <c>ArgumentList</c>.</param>
/// <param name="WorkingDirectory">The absolute directory to run in. It must exist.</param>
public sealed record OmpLaunchRequest(string ExecutablePath, IReadOnlyList<string> Arguments, string WorkingDirectory);
