namespace OmpOS.Harness.Terminal;

/// <summary>
/// A command the agent wants to run in the workspace terminal. The command is an executable and an argument list;
/// no shell interprets it.
/// </summary>
/// <param name="Command">The executable name or path, resolved by the operating system as <c>ProcessStartInfo.FileName</c>.</param>
/// <param name="Arguments">The arguments, passed one element each through <c>ProcessStartInfo.ArgumentList</c>.</param>
/// <param name="WorkingDirectory">
/// The directory to run in: relative to the workspace root, or an absolute path inside it. Empty means the workspace root.
/// </param>
public sealed record CommandProposal(string Command, IReadOnlyList<string> Arguments, string WorkingDirectory = "");
