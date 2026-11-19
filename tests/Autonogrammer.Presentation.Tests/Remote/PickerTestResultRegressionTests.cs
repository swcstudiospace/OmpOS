using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The New session picker gates an environment on the same test outcome the Remote page shows: a failed test blocks it, and the picker follows the page's tests.</summary>
public sealed class PickerTestResultRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Ssh() => new()
    {
        Id = "a",
        DisplayName = "Box a",
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/home/dev/work" },
    };

    private static EnvironmentProfile Container() => new()
    {
        Id = "devbox",
        DisplayName = "Box devbox",
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = "devbox", RemoteUser = "dev", WorkspaceFolder = "/workspaces/app" },
    };

    private static RemotePreflightReport Failed() => new()
    {
        Succeeded = false,
        Diagnoses = [new RemoteDiagnosis { Kind = RemoteDiagnosisKind.OmpMissing, Severity = DiagnosisSeverity.Error, Summary = "omp is missing.", FixText = "Install omp.", Detail = "ssh said no" }],
    };

    /// <summary>A failed connection test on the page blocks the environment in the picker, which shows the page's status.</summary>
    [Fact]
    public async Task AFailedSshTestOnThePageBlocksTheEnvironmentInThePicker()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Ssh());
        host.Report = Failed();
        var page = new RemoteEnvironmentsViewModel(host);
        var picker = new NewSessionTargetModel([Ssh()]);
        picker.SetStatusSource(page.StatusOf);
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        RemoteStatusDescriber.BlocksStart(page.Rows[0].Status).ShouldBeTrue();
        picker.Select("ssh:a");
        picker.CanStart.ShouldBeFalse();
        picker.Validation.Message.ShouldStartWith(RemoteCopy.TargetBlockedPrefix, Case.Sensitive);
        picker.Validation.Message.ShouldContain(page.Rows[0].StatusLabel, Case.Sensitive);
        picker.TryGetTarget(out _).ShouldBeFalse();
    }

    /// <summary>The picker refreshes its labels when the page reports a finished test, without anyone asking it to.</summary>
    [Fact]
    public async Task ThePickerFollowsTheTestsOfThePage()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Ssh());
        var page = new RemoteEnvironmentsViewModel(host);
        var picker = new NewSessionTargetModel([Ssh()]);
        picker.SetStatusSource(page.StatusOf);
        page.StatusesChanged += (_, _) => picker.RefreshStatuses();
        await page.LoadAsync(Token).ConfigureAwait(true);
        picker.Select("ssh:a");

        host.Report = Failed();
        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        picker.Options[1].StatusLabel.ShouldBe(page.Rows[0].StatusLabel);
        picker.CanStart.ShouldBeFalse();
    }

    /// <summary>An environment that passed its test, or was never tested, starts as it did before the picker read the page.</summary>
    [Fact]
    public async Task APassingOrUntestedEnvironmentStillStarts()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Ssh());
        var page = new RemoteEnvironmentsViewModel(host);
        var picker = new NewSessionTargetModel([Ssh()]);
        picker.SetStatusSource(page.StatusOf);
        await page.LoadAsync(Token).ConfigureAwait(true);
        picker.Select("ssh:a");

        picker.CanStart.ShouldBeTrue();
        picker.Validation.Message.ShouldBe(RemoteCopy.TargetUntested);

        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        picker.CanStart.ShouldBeTrue();
        picker.Validation.Message.ShouldBe(RemoteCopy.TargetOk);
    }

    /// <summary>A failed container test on the Containers page blocks the container in the picker, through the same page model the picker reads.</summary>
    [Fact]
    public async Task AFailedContainerTestOnThePageBlocksTheContainerInThePicker()
    {
        var store = new FakeRemoteEnvironmentsHost().With(Container());
        var runtime = new FakeContainerEnvironmentsHost();
        runtime.ContainerReport = Failed();
        var page = new RuntimeEnvironmentsViewModel(store, runtime, new FakeContainerLister());
        var picker = new NewSessionTargetModel([Container()]);
        picker.SetStatusSource(page.StatusOf);
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        picker.Select("container:devbox");
        picker.CanStart.ShouldBeFalse();
        picker.Validation.Message.ShouldContain(page.Rows[0].StatusLabel, Case.Sensitive);
    }
}
