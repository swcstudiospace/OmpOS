namespace OmpOS.Contracts.Environments;

/// <summary>One reason a profile is not acceptable.</summary>
/// <param name="Property">The dotted name of the offending property, for example <c>Ssh.RemoteWorkspaceRoot</c>.</param>
/// <param name="Message">A plain-text explanation that never contains the rejected value.</param>
public sealed record ProfileIssue(string Property, string Message);
