namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// One folder of the set being picked, as plain data the picker exchanges with its backend.
/// </summary>
/// <param name="Id">The identifier of the root in the set.</param>
/// <param name="Path">The absolute folder path.</param>
/// <param name="DisplayName">The name shown to the user.</param>
/// <param name="EnvironmentId">The environment that owns the folder, or null for this machine.</param>
/// <param name="IsPrimary">True for the working directory of the run. Exactly one row of a non-empty picker is primary.</param>
public sealed record WorkspacePickerRoot(string Id, string Path, string DisplayName, string? EnvironmentId, bool IsPrimary);
