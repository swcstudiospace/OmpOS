using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>Every remote status has its own words and an emphasis, and a test report or a live connection state maps to the status a user can act on.</summary>
public sealed class RemoteStatusDescriberTests
{
    private static RemotePreflightReport Failed(params RemoteDiagnosisKind[] kinds) => new()
    {
        Succeeded = false,
        Diagnoses = [.. kinds.Select(kind => new RemoteDiagnosis { Kind = kind, Severity = DiagnosisSeverity.Error, Summary = "s", FixText = "f" })],
    };

    /// <summary>No two statuses share a label, and every status has a label and a sentence of its own: a state is never said by colour alone.</summary>
    [Fact]
    public void EveryStatusHasItsOwnWords()
    {
        var statuses = Enum.GetValues<RemoteEnvironmentStatus>();

        statuses.Select(RemoteStatusDescriber.LabelOf).ShouldBeUnique(StringComparer.Ordinal);
        statuses.Select(status => RemoteStatusDescriber.DetailOf(status)).ShouldBeUnique(StringComparer.Ordinal);
        statuses.ShouldAllBe(status => RemoteStatusDescriber.LabelOf(status).Length > 0 && RemoteStatusDescriber.DetailOf(status).Length > 0);
    }

    /// <summary>The failures the user must fix before a session can start block it; the working and in-progress states do not.</summary>
    [Fact]
    public void OnlyFailedTestOutcomesBlockStart()
    {
        Enum.GetValues<RemoteEnvironmentStatus>().Where(RemoteStatusDescriber.BlocksStart).ShouldBe(
        [
            RemoteEnvironmentStatus.HostKeyUnconfirmed,
            RemoteEnvironmentStatus.HostKeyChanged,
            RemoteEnvironmentStatus.AuthFailed,
            RemoteEnvironmentStatus.Unreachable,
            RemoteEnvironmentStatus.VersionMismatch,
            RemoteEnvironmentStatus.NeedsSetup,
            RemoteEnvironmentStatus.RuntimeUnavailable,
            RemoteEnvironmentStatus.NotRunning,
            RemoteEnvironmentStatus.NotFound,
        ]);
    }

    /// <summary>Success states are good, recoveries and caveats are warnings, failures are errors, steps in progress are progress.</summary>
    [Fact]
    public void SeverityFollowsTheMeaning()
    {
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.Ready).ShouldBe(RemoteStatusSeverity.Good);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.Connected).ShouldBe(RemoteStatusSeverity.Good);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.Reconnecting).ShouldBe(RemoteStatusSeverity.Warning);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.Interrupted).ShouldBe(RemoteStatusSeverity.Warning);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.AuthFailed).ShouldBe(RemoteStatusSeverity.Error);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.Testing).ShouldBe(RemoteStatusSeverity.Progress);
        RemoteStatusDescriber.SeverityOf(RemoteEnvironmentStatus.NotChecked).ShouldBe(RemoteStatusSeverity.Neutral);
    }

    /// <summary>A report maps to the status of its first error: host key, sign-in, reachability, omp version, or setup.</summary>
    [Theory]
    [InlineData(RemoteDiagnosisKind.HostKeyUnknown, RemoteEnvironmentStatus.HostKeyUnconfirmed)]
    [InlineData(RemoteDiagnosisKind.HostKeyChanged, RemoteEnvironmentStatus.HostKeyChanged)]
    [InlineData(RemoteDiagnosisKind.AuthFailed, RemoteEnvironmentStatus.AuthFailed)]
    [InlineData(RemoteDiagnosisKind.HostUnreachable, RemoteEnvironmentStatus.Unreachable)]
    [InlineData(RemoteDiagnosisKind.SshMissing, RemoteEnvironmentStatus.Unreachable)]
    [InlineData(RemoteDiagnosisKind.OmpMissing, RemoteEnvironmentStatus.VersionMismatch)]
    [InlineData(RemoteDiagnosisKind.OmpTooOld, RemoteEnvironmentStatus.VersionMismatch)]
    [InlineData(RemoteDiagnosisKind.BunMissing, RemoteEnvironmentStatus.VersionMismatch)]
    [InlineData(RemoteDiagnosisKind.ShellNotPosix, RemoteEnvironmentStatus.NeedsSetup)]
    [InlineData(RemoteDiagnosisKind.DirectoryMissing, RemoteEnvironmentStatus.NeedsSetup)]
    [InlineData(RemoteDiagnosisKind.Unknown, RemoteEnvironmentStatus.NeedsSetup)]
    public void AReportMapsToTheStatusOfItsFirstError(RemoteDiagnosisKind kind, RemoteEnvironmentStatus expected) =>
        RemoteStatusDescriber.FromReport(Failed(kind)).ShouldBe(expected);

    /// <summary>The first error decides when there are several, and a clean report is Ready while one with only warnings is Ready with warnings.</summary>
    [Fact]
    public void ThePassingOutcomesAndTheFirstErrorDecide()
    {
        RemoteStatusDescriber.FromReport(Failed(RemoteDiagnosisKind.AuthFailed, RemoteDiagnosisKind.OmpMissing)).ShouldBe(RemoteEnvironmentStatus.AuthFailed);
        RemoteStatusDescriber.FromReport(new RemotePreflightReport { Succeeded = true, Diagnoses = [] }).ShouldBe(RemoteEnvironmentStatus.Ready);
        var warning = new RemoteDiagnosis { Kind = RemoteDiagnosisKind.UnsupportedPlatform, Severity = DiagnosisSeverity.Warning, Summary = "s", FixText = "f" };
        RemoteStatusDescriber.FromReport(new RemotePreflightReport { Succeeded = true, Diagnoses = [warning] }).ShouldBe(RemoteEnvironmentStatus.ReadyWithWarnings);
        RemoteStatusDescriber.FromReport(new RemotePreflightReport { Succeeded = false, Diagnoses = [warning] }).ShouldBe(RemoteEnvironmentStatus.NeedsSetup);
        Should.Throw<ArgumentNullException>(() => RemoteStatusDescriber.FromReport(null!));
    }

    /// <summary>A live connection state maps to its status, and a lost connection with a running turn is Interrupted whatever the retry state is.</summary>
    [Fact]
    public void ALiveConnectionMapsToItsStatus()
    {
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Connecting, false).ShouldBe(RemoteEnvironmentStatus.Connecting);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Connected, false).ShouldBe(RemoteEnvironmentStatus.Connected);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Reconnecting, false).ShouldBe(RemoteEnvironmentStatus.Reconnecting);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Unreachable, false).ShouldBe(RemoteEnvironmentStatus.Unreachable);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Closed, false).ShouldBe(RemoteEnvironmentStatus.Disconnected);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Disconnected, false).ShouldBe(RemoteEnvironmentStatus.Disconnected);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Disconnected, true).ShouldBe(RemoteEnvironmentStatus.Interrupted);
        RemoteStatusDescriber.FromConnection(RemoteConnectionState.Unreachable, true).ShouldBe(RemoteEnvironmentStatus.Interrupted);
    }

    /// <summary>The reconnect sentence names the attempt when it is known.</summary>
    [Fact]
    public void TheReconnectSentenceNamesTheAttempt()
    {
        RemoteStatusDescriber.DetailOf(RemoteEnvironmentStatus.Reconnecting, 3).ShouldContain("attempt 3", Case.Sensitive);
        RemoteStatusDescriber.DetailOf(RemoteEnvironmentStatus.Reconnecting).ShouldNotContain("attempt", Case.Sensitive);
    }
}
