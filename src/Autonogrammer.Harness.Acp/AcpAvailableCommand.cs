namespace Autonogrammer.Harness.Acp;

/// <summary>One command the agent advertised in an <c>available_commands_update</c> notification.</summary>
/// <param name="Name">The command name without the leading slash.</param>
/// <param name="Description">The description the agent gave the command, or <see langword="null"/>.</param>
/// <param name="InputHint">The hint the agent gave for the command's input (<c>input.hint</c>), or <see langword="null"/> when the command takes no input.</param>
public sealed record AcpAvailableCommand(string Name, string? Description, string? InputHint);
