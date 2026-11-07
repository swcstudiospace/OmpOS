using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The page's failure lines and status line: an unreachable service is announced once, and only a running command offers Cancel.</summary>
public sealed class DispatchPageFailuresTests
{
    private static DispatchHostException Unavailable() => new(DispatchHostFailureKind.Unavailable, "The service is down.");

    /// <summary>An unreachable service and a problem with the same text are announced as one line.</summary>
    [Fact]
    public void AnUnreachableServiceAndItsProblemAreOneFailureLine()
    {
        var devices = DevicesViewModel.Failed(Unavailable());
        var page = new DispatchPageModel(devices, [], PairThisDevice: null, Problem: devices.StatusText);

        page.Failures.ShouldBe([devices.StatusText]);
    }

    /// <summary>The failure lines run in order: the problem, then each failed command.</summary>
    [Fact]
    public void TheProblemComesBeforeTheFailedCommands()
    {
        var failed = DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Failed, "Fix the build", DispatchFailureReason.Timeout);
        var page = new DispatchPageModel(DevicesViewModel.Loaded([]), [failed], PairThisDevice: null, Problem: "The list could not be read.");

        page.Failures.ShouldBe(["The list could not be read.", DispatchCopy.ForFailure(DispatchFailureReason.Timeout)]);
    }

    /// <summary>The status line is empty while the service is unreachable, because the failure line already says so.</summary>
    [Fact]
    public void TheStatusLineIsEmptyWhileTheServiceIsUnreachable()
    {
        DevicesViewModel.Failed(Unavailable()).StatusLine.ShouldBeEmpty();
        DevicesViewModel.Loaded([]).StatusLine.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.NoDevices));
    }

    /// <summary>Only a running command offers Cancel; a queued or finished command does not.</summary>
    [Fact]
    public void OnlyARunningCommandOffersCancel()
    {
        var rows = new[]
        {
            DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Running, "A"),
            DispatchCommandRowModel.From("command-2", "run", DispatchPhase.Queued, "B"),
            DispatchCommandRowModel.From("command-3", "run", DispatchPhase.Completed, "C"),
        };

        rows.Select(row => row.CanCancel).ShouldBe([true, false, false]);
    }
}
