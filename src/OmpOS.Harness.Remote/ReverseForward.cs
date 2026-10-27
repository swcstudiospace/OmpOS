namespace OmpOS.Harness.Remote;

/// <summary>
/// A reverse port forward (<c>ssh -R</c>): a loopback port on the remote machine that reaches a service on this machine. It lets the
/// remote omp use a local MCP server through <c>127.0.0.1</c> without exposing the service to the network.
/// </summary>
/// <param name="RemotePort">The port the remote machine listens on, on its loopback address only.</param>
/// <param name="LocalHost">The host this machine connects to: <c>127.0.0.1</c>, <c>localhost</c> or a bracketed IPv6 loopback literal.</param>
/// <param name="LocalPort">The port this machine connects to.</param>
public sealed record ReverseForward(int RemotePort, string LocalHost, int LocalPort);
