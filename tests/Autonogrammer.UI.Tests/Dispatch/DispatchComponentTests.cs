using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.UI.Components.Dispatch;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Dispatch;

/// <summary>The Dispatch components: each state has its text, failures are announced assertively once, the approval prompt has its three named actions, Stop is a named button, a running command offers a named Cancel, and the page announces politely through one region.</summary>
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

    /// <summary>An unreachable service is announced once in the failure region, and the status line does not repeat it.</summary>
    [Fact]
    public void AnUnreachableServiceIsNotRepeatedInTheStatusLine()
    {
        var devices = DevicesViewModel.Failed(new DispatchHostException(DispatchHostFailureKind.Unavailable, "The service is down."));
        var page = new DispatchPageModel(devices, [], PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, page));

        cut.Find(".dispatch-status").TextContent.ShouldBeEmpty();
        cut.FindAll(".dispatch-failures p").Select(line => line.TextContent).ToArray().ShouldBe([DispatchCopy.ForPhase(DispatchPhase.Unreachable)]);
    }

    /// <summary>A running command offers a named Cancel, and pressing it raises the command's identifier.</summary>
    [Fact]
    public void ARunningCommandOffersANamedCancelThatRaisesItsIdentifier()
    {
        string? cancelled = null;
        var page = new DispatchPageModel(
            DevicesViewModel.Loaded([]),
            [DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Running, "Fix the build")],
            PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters
            .Add(p => p.Model, page)
            .Add(p => p.OnCancel, EventCallback.Factory.Create<string>(this, commandId => cancelled = commandId)));

        var cancel = cut.Find("button.dispatch-cancel");
        cancel.TextContent.Trim().ShouldBe(DispatchCopy.CancelLabel);
        cancel.GetAttribute("aria-label").ShouldBe($"{DispatchCopy.CancelLabel}: Fix the build");

        cancel.Click();
        cancelled.ShouldBe("command-1");
    }

    /// <summary>Only a running command offers Cancel; a queued or finished command does not.</summary>
    [Fact]
    public void OnlyARunningCommandOffersCancel()
    {
        var page = new DispatchPageModel(
            DevicesViewModel.Loaded([]),
            [
                DispatchCommandRowModel.From("command-1", "run", DispatchPhase.Queued, "Queued task"),
                DispatchCommandRowModel.From("command-2", "run", DispatchPhase.Completed, "Done task"),
            ],
            PairThisDevice: null);

        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, page));

        cut.FindAll("button.dispatch-cancel").ShouldBeEmpty();
    }

    /// <summary>The page heading takes focus programmatically, so a cancel can move focus to it.</summary>
    [Fact]
    public void ThePageHeadingCanTakeFocus()
    {
        var cut = Render<DispatchPageView>(parameters => parameters.Add(p => p.Model, EmptyPage()));

        cut.Find("#dispatch-page-heading").GetAttribute("tabindex").ShouldBe("-1");
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
