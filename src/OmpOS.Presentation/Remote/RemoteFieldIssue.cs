namespace OmpOS.Presentation.Remote;

/// <summary>One reason the environment being edited cannot be saved, tied to the field that has to change.</summary>
/// <param name="Field">The name of the editor property that is wrong, for example <c>RemoteWorkspaceRoot</c>.</param>
/// <param name="Message">A plain sentence that never contains the rejected value.</param>
public sealed record RemoteFieldIssue(string Field, string Message);
