namespace Autonogrammer.Contracts.Environments;

/// <summary>The result of testing a remote environment: what was found on the machine and what, if anything, must be fixed.</summary>
public sealed record RemotePreflightReport
{
    /// <summary>Gets whether a session can run: no diagnosis has <see cref="DiagnosisSeverity.Error"/>.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Gets the findings, errors first. Empty when everything checked out.</summary>
    public required IReadOnlyList<RemoteDiagnosis> Diagnoses { get; init; }

    /// <summary>Gets the home directory of the remote user, or <see langword="null"/> when it could not be read.</summary>
    public string? HomeDirectory { get; init; }

    /// <summary>Gets the remote operating system and CPU as <c>uname -sm</c> prints them, or <see langword="null"/>.</summary>
    public string? Platform { get; init; }

    /// <summary>Gets the resolved path of the remote omp, or <see langword="null"/> when it was not found.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the version of the remote omp (for example <c>18.12.6</c>), or <see langword="null"/>.</summary>
    public string? OmpVersion { get; init; }
}
