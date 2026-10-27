namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// A context attachment of a prompt or a file read, as Cursor sends it in <c>attachments</c>.
/// </summary>
/// <param name="Type">Either <c>file</c> or <c>rule</c>.</param>
/// <param name="FilePath">The absolute path of the attachment.</param>
public sealed record HookAttachment(string Type, string FilePath);
