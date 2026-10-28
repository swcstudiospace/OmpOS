namespace Autonogrammer.Presentation.Hooks;

/// <summary>One problem found while reading a hook file: the file, the place in it and the reason.</summary>
/// <param name="File">The file the problem was found in.</param>
/// <param name="Location">The JSON path of the problem, for example <c>$.hooks.stop[0]</c>.</param>
/// <param name="Reason">What is wrong.</param>
/// <param name="IsError">True when the problem makes the file or the hook unusable; false for a warning.</param>
public sealed record HookDiagnosticItem(string File, string Location, string Reason, bool IsError)
{
    /// <summary>Gets the problem as one sentence for an accessible name: the file, the place and the reason.</summary>
    public string Summary => $"{File} at {Location}: {Reason}";
}
