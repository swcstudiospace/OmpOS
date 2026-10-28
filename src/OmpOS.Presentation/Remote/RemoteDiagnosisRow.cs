using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>One finding of a connection test as the page lists it: how serious it is in words, what is wrong, and how to fix it.</summary>
public sealed record RemoteDiagnosisRow
{
    /// <summary>Gets <c>Error</c> or <c>Warning</c>, in words so the severity never depends on colour.</summary>
    public required string SeverityLabel { get; init; }

    /// <summary>Gets whether the finding stops a session from starting.</summary>
    public required bool IsError { get; init; }

    /// <summary>Gets the one-sentence statement of the problem.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the steps that fix the problem.</summary>
    public required string FixText { get; init; }

    /// <summary>Gets the first lines the failing tool printed, or an empty string.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Gets whether <see cref="Detail"/> has text.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>Gets the text a screen reader announces for the row: severity, problem and fix.</summary>
    public string AccessibleText => SeverityLabel + ": " + Summary + " " + RemoteCopy.FixLabel + ": " + FixText;

    /// <summary>Makes the row of a diagnosis.</summary>
    /// <param name="diagnosis">The diagnosis.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="diagnosis"/> is null.</exception>
    public static RemoteDiagnosisRow From(RemoteDiagnosis diagnosis)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);
        var isError = diagnosis.Severity == DiagnosisSeverity.Error;
        return new RemoteDiagnosisRow
        {
            SeverityLabel = isError ? RemoteCopy.ErrorLabel : RemoteCopy.WarningLabel,
            IsError = isError,
            Summary = diagnosis.Summary,
            FixText = diagnosis.FixText,
            Detail = diagnosis.Detail ?? string.Empty,
        };
    }
}
