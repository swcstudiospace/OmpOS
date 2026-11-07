using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The Dispatch page, command, approval and strip models: the phase each state shows, and the capped plain task text.</summary>
public sealed class DispatchPageModelTests
{
    /// <summary>An account with no devices shows the no-devices phase and no rows.</summary>
    [Fact]
    public void AnAccountWithNoDevicesShowsTheNoDevicesPhase()
    {
        var devices = DevicesViewModel.Loaded([]);

        devices.Phase.ShouldBe(DispatchPhase.NoDevices);
        devices.Rows.ShouldBeEmpty();
    }

    /// <summary>Loading is a state of its own, with no rows.</summary>
    [Fact]
    public void LoadingIsTheLoadingPhaseWithNoRows()
    {
        var devices = DevicesViewModel.Loading();

        devices.Phase.ShouldBe(DispatchPhase.Loading);
        devices.Rows.ShouldBeEmpty();
    }

    /// <summary>A feature that is off shows Disabled and a signed-out user shows SignedOut; an answered state is never a spinner.</summary>
    [Theory]
    [InlineData(DispatchHostFailureKind.NotConfigured, DispatchPhase.Disabled)]
    [InlineData(DispatchHostFailureKind.Disabled, DispatchPhase.Disabled)]
    [InlineData(DispatchHostFailureKind.SignedOut, DispatchPhase.SignedOut)]
    [InlineData(DispatchHostFailureKind.Unavailable, DispatchPhase.Unreachable)]
    public void AHostFailureShowsThePhaseItMeans(DispatchHostFailureKind kind, DispatchPhase expected)
    {
        var devices = DevicesViewModel.Failed(new DispatchHostException(kind, "unavailable"));

        devices.Phase.ShouldBe(expected);
        devices.Phase.ShouldNotBe(DispatchPhase.Loading);
    }

    /// <summary>A failed command shows the text for its reason, not a generic line.</summary>
    [Fact]
    public void AFailedCommandShowsItsFailureReasonText()
    {
        var row = DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Failed, "Fix the build", DispatchFailureReason.Timeout);

        row.StatusText.ShouldBe(DispatchCopy.ForFailure(DispatchFailureReason.Timeout));
    }

    /// <summary>The approval prompt shows the first two hundred characters of the task text, and its device name and tool classes unchanged.</summary>
    [Fact]
    public void TheApprovalPromptCapsTheTaskText()
    {
        var text = new string('b', 300);

        var prompt = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], text);

        prompt.TaskText.Length.ShouldBe(200);
        prompt.DeviceName.ShouldBe("Office Mac");
        prompt.ToolClasses.ShouldBe(["shell"]);
    }

    /// <summary>The remote task strip names its controller and offers Stop while the task runs.</summary>
    [Fact]
    public void TheRemoteTaskStripNamesTheControllerAndOffersStop()
    {
        var strip = RemoteTaskStripModel.From("Laptop", "Run the tests");

        strip.ControllerName.ShouldBe("Laptop");
        strip.CanStop.ShouldBeTrue();
    }

    /// <summary>A head with no local device has no pairing section; the web head is such a head.</summary>
    [Fact]
    public void APageWithoutALocalDeviceHasNoPairSection()
    {
        var page = new DispatchPageModel(DevicesViewModel.Loaded([]), [], PairThisDevice: null);

        page.PairThisDevice.ShouldBeNull();
    }
}
