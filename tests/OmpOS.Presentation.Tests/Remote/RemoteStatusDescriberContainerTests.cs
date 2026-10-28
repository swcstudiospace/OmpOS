using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The status words for container and WSL environments: what each diagnosis becomes, how serious it is, and whether it blocks a start.</summary>
public sealed class RemoteStatusDescriberContainerTests
{
    private static RemotePreflightReport Failure(RemoteDiagnosisKind kind) => new()
    {
        Succeeded = false,
        Diagnoses = [new RemoteDiagnosis { Kind = kind, Severity = DiagnosisSeverity.Error, Summary = "s", FixText = "f" }],
    };

    /// <summary>Every container and WSL diagnosis maps to the status that says what to do.</summary>
    [Theory]
    [InlineData(RemoteDiagnosisKind.ContainerRuntimeMissing, RemoteEnvironmentStatus.RuntimeUnavailable)]
    [InlineData(RemoteDiagnosisKind.ContainerRuntimeUnavailable, RemoteEnvironmentStatus.RuntimeUnavailable)]
    [InlineData(RemoteDiagnosisKind.WslMissing, RemoteEnvironmentStatus.RuntimeUnavailable)]
    [InlineData(RemoteDiagnosisKind.ContainerNotRunning, RemoteEnvironmentStatus.NotRunning)]
    [InlineData(RemoteDiagnosisKind.ContainerMissing, RemoteEnvironmentStatus.NotFound)]
    [InlineData(RemoteDiagnosisKind.WslDistroMissing, RemoteEnvironmentStatus.NotFound)]
    [InlineData(RemoteDiagnosisKind.ContainerUserMissing, RemoteEnvironmentStatus.NeedsSetup)]
    [InlineData(RemoteDiagnosisKind.WslUserMissing, RemoteEnvironmentStatus.NeedsSetup)]
    [InlineData(RemoteDiagnosisKind.DirectoryMissing, RemoteEnvironmentStatus.NeedsSetup)]
    [InlineData(RemoteDiagnosisKind.OmpMissing, RemoteEnvironmentStatus.VersionMismatch)]
    public void ReportsMapToStatuses(RemoteDiagnosisKind kind, RemoteEnvironmentStatus expected)
    {
        RemoteStatusDescriber.FromReport(Failure(kind)).ShouldBe(expected);
    }

    /// <summary>A stopped container is a warning (an action fixes it) that blocks a start; a missing runtime or container is an error that blocks a start.</summary>
    [Fact]
    public void SeverityAndBlockingFollowTheMeaning()
    {
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.NotRunning).ShouldBe(RemoteStatusSeverity.Warning);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.RuntimeUnavailable).ShouldBe(RemoteStatusSeverity.Error);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.NotFound).ShouldBe(RemoteStatusSeverity.Error);
        RemoteStatusDescriber.BlocksStart(RemoteEnvironmentStatus.NotRunning).ShouldBeTrue();
        RemoteStatusDescriber.BlocksStart(RemoteEnvironmentStatus.RuntimeUnavailable).ShouldBeTrue();
        RemoteStatusDescriber.BlocksStart(RemoteEnvironmentStatus.NotFound).ShouldBeTrue();
    }

    /// <summary>The detail of a stopped container says that OmpOS never starts it on its own; every new status has words of its own.</summary>
    [Fact]
    public void TheWordsAreDistinct()
    {
        RemoteStatusDescriber.DetailOf(RemoteEnvironmentStatus.NotRunning).ShouldContain("never starts a container on its own", Case.Sensitive);
        new[] { RemoteEnvironmentStatus.RuntimeUnavailable, RemoteEnvironmentStatus.NotRunning, RemoteEnvironmentStatus.NotFound }
            .Select(RemoteStatusDescriber.LabelOf).ShouldBeUnique(StringComparer.Ordinal);
    }
}
