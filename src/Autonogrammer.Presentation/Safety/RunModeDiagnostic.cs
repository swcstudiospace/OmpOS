namespace Autonogrammer.Presentation.Safety;

/// <summary>A problem found in a permissions or sandbox file. The file is never changed because of it.</summary>
/// <param name="File">The file the problem is in.</param>
/// <param name="Message">What is wrong and what was done about it.</param>
/// <param name="IsError">True when the whole file was ignored, false when only an entry was.</param>
public sealed record RunModeDiagnostic(string File, string Message, bool IsError)
{
    /// <summary>Gets the line as one sentence for an accessible name.</summary>
    public string Summary => (IsError ? "Error in " : "Warning in ") + File + ": " + Message;
}
