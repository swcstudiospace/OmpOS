using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.UI.Components.Dispatch;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Dispatch;

/// <summary>The Dispatch components: each state has its text, failures are announced assertively, the approval prompt has its three named actions, Stop is a named button, and the page announces politely through one region.</summary>
public sealed class DispatchComponentTests : UiTestContext
{
    private static DispatchPageModel EmptyPage() => new(DevicesViewModel.Loaded([]), [], PairThisDevice: null);

    /// <summary>An account with no devices shows the no-devices text and no device list.</summary>
    [Fact]
    public void ThePageShowsTheNoDevicesPhaseText()
    {
        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, EmptyPage()));

        cut.Markup.ShouldContain(DispatchCopy.ForPhase(DispatchPhase.NoDevices));
        cut.FindAll($"[aria-label='{DispatchCopy.DevicesLabel}']").ShouldBeEmpty();
    }

    /// <summary>A failed command shows the text for its failure reason.</summary>
    [Fact]
    public void AFailedCommandShowsItsFailureReason()
    {
        var row = DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Failed, "Fix the build", DispatchFailureReason.Timeout);
        var page = new DispatchPageModel(DevicesViewModel.Loaded([]), [row], PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, page));

        cut.Markup.ShouldContain(DispatchCopy.ForFailure(DispatchFailureReason.Timeout));
    }

    /// <summary>A failed command is announced through the assertive region, with the text for its failure reason.</summary>
    [Fact]
    public void AFailedCommandIsAnnouncedAssertively()
    {
        var row = DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Failed, "Fix the build", DispatchFailureReason.Timeout);
        var page = new DispatchPageModel(DevicesViewModel.Loaded([]), [row], PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, page));

        cut.Find("[aria-live='assertive']").TextContent.ShouldContain(DispatchCopy.ForFailure(DispatchFailureReason.Timeout));
    }

    /// <summary>An unreachable service is announced through the assertive region, with the text for its phase.</summary>
    [Fact]
    public void AnUnreachableServiceIsAnnouncedAssertively()
    {
        var devices = DevicesViewModel.Failed(new DispatchHostException(DispatchHostFailureKind.Unavailable, "The service is down."));
        var page = new DispatchPageModel(devices, [], PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, page));

        cut.Find("[aria-live='assertive']").TextContent.ShouldContain(DispatchCopy.ForPhase(DispatchPhase.Unreachable));
    }

    /// <summary>The approval prompt names its three actions in order, and Approve once raises its callback.</summary>
    [Fact]
    public void TheApprovalPromptOffersApproveOnceDenyAndDenyAndPause()
    {
        var approved = false;
        var approval = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");

        var cut = Render<DispatchApprovalPanel>(parameters => parameters
            .Add(p => p.Model, approval)
            .Add(p => p.OnApprove, EventCallback.Factory.Create(this, () => approved = true)));

        var labels = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToArray();
        labels.ShouldBe([DispatchCopy.ApproveOnceLabel, DispatchCopy.DenyLabel, DispatchCopy.DenyAndPauseLabel]);

        cut.Find("button").Click();
        approved.ShouldBeTrue();
    }

    /// <summary>The remote task strip names its controller, and its Stop button is named and raises its callback.</summary>
    [Fact]
    public void TheRemoteTaskStripOffersANamedStop()
    {
        var stopped = false;
        var strip = RemoteTaskStripModel.From("Laptop", "Run the tests");

        var cut = Render<RemoteTaskStrip>(parameters => parameters
            .Add(p => p.Model, strip)
            .Add(p => p.OnStop, EventCallback.Factory.Create(this, () => stopped = true)));

        cut.Markup.ShouldContain("Laptop");
        var stop = cut.Find("button");
        stop.TextContent.Trim().ShouldBe(DispatchCopy.StopLabel);

        stop.Click();
        stopped.ShouldBeTrue();
    }

    /// <summary>The page announces its transient message through one polite live region.</summary>
    [Fact]
    public void ThePageAnnouncesPolitely()
    {
        var cut = Render<DispatchPageView>(parameters => parameters
            .Add(p => p.Model, EmptyPage())
            .Add(p => p.Announcement, "Command sent."));

        cut.Find("[aria-live='polite']").TextContent.ShouldBe("Command sent.");
    }
}
