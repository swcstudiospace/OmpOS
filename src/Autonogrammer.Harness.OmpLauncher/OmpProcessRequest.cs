namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// One command for <see cref="IOmpProcessRunner"/>. No shell is involved: the file runs directly with the listed arguments.
/// </summary>
/// <param name="FileName">The absolute path of the file to run.</param>
/// <param name="Arguments">The arguments, one element each.</param>
/// <param name="WorkingDirectory">The absolute directory to run in. It must exist.</param>
/// <param name="Timeout">The time allowed before the process tree is killed.</param>
public sealed record OmpProcessRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, TimeSpan Timeout);
