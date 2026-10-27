namespace OmpOS.Contracts.Environments;

/// <summary>One finding about a remote environment, with the text that tells the user what to do. It is plain text and holds no secret.</summary>
public sealed record RemoteDiagnosis
{
    /// <summary>Gets what is wrong.</summary>
    public required RemoteDiagnosisKind Kind { get; init; }

    /// <summary>Gets how serious it is.</summary>
    public required DiagnosisSeverity Severity { get; init; }

    /// <summary>Gets a one-sentence statement of the problem.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the steps that fix the problem.</summary>
    public required string FixText { get; init; }

    /// <summary>Gets the first lines the failing tool printed (bounded, secrets removed), or <see langword="null"/>.</summary>
    public string? Detail { get; init; }
}
