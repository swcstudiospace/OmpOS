namespace OmpOS.Presentation.Settings.Ignore;

/// <summary>An ignore file found in the workspace.</summary>
/// <param name="Name">The file path relative to the workspace root.</param>
/// <param name="RuleCount">The number of rules the file holds.</param>
/// <param name="Problems">What was skipped in the file and why, one sentence each. Empty when the file is fine.</param>
public sealed record IgnoreFileRow(string Name, int RuleCount, IReadOnlyList<string> Problems)
{
    /// <summary>Gets the whole row as one sentence for the accessible name.</summary>
    public string Summary => IgnoreCopy.FileSummary(Name, RuleCount, Problems.Count);

    /// <summary>Gets a value indicating whether the file has problems.</summary>
    public bool HasProblems => Problems.Count > 0;
}
