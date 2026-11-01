using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Notifications.Tests;

/// <summary>Focus suppression, the 30 second coalesce, the 10 per minute cap, Do-not-disturb and title-only privacy.</summary>
public sealed class NotificationPolicyTests
{
    /// <summary>Identical requests inside 30 seconds become one operating-system delivery. The in-app centre keeps every one.</summary>
    [Fact]
    public void IdenticalRequestsInsideThirtySecondsCoalesceWithoutLosingInAppItems()
    {
        var clock = new ManualClock();
        var policy = new NotificationPolicy(clock);
        var request = new NotificationRequest(NotificationKind.RunFinished, "Run finished", "The notes", "session-1");

        var deliveries = 0;
        for (var i = 0; i < 3; i++)
        {
            var receipt = policy.Publish(request, windowFocused: false, doNotDisturb: false);
            if (receipt.DeliverToOperatingSystem)
            {
                deliveries++;
            }

            receipt.RetainedInApp.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(5));
        }

        deliveries.ShouldBe(1);
        policy.InApp.Count.ShouldBe(3);
        policy.InApp.ShouldAllBe(item => item == request);
        policy.OperatingSystemDrops.ShouldBe(0);
    }

    /// <summary>The 11th distinct request inside one minute is dropped from the operating-system surface and counted. The in-app list keeps it.</summary>
    [Fact]
    public void TheEleventhRequestInsideOneMinuteIsDroppedFromTheOperatingSystemAndCounted()
    {
        var clock = new ManualClock();
        var policy = new NotificationPolicy(clock);
        var deliveries = 0;
        NotificationReceipt? last = null;
        for (var i = 1; i <= 11; i++)
        {
            var request = new NotificationRequest(NotificationKind.ScheduledTaskFinished, "Task " + i, "body " + i, "session-1");
            last = policy.Publish(request, windowFocused: false, doNotDisturb: false);
            if (last.DeliverToOperatingSystem)
            {
                deliveries++;
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        deliveries.ShouldBe(10);
        last.ShouldNotBeNull().DeliverToOperatingSystem.ShouldBeFalse();
        last.OperatingSystemDrops.ShouldBe(1);
        policy.OperatingSystemDrops.ShouldBe(1);
        policy.InApp.Count.ShouldBe(11);
    }

    /// <summary>A focused window and Do-not-disturb suppress the operating-system surface. Title only is the default. Show content reveals the summary.</summary>
    [Fact]
    public void FocusAndDoNotDisturbSuppressAndShowContentRevealsTheSummary()
    {
        var policy = new NotificationPolicy(new ManualClock());
        var request = new NotificationRequest(NotificationKind.ApprovalNeeded, "Approval needed", "Deploy the patch", "session-9", approvalEntryId: "queue-9");

        var focused = policy.Publish(request, windowFocused: true, doNotDisturb: false);
        focused.DeliverToOperatingSystem.ShouldBeFalse();
        focused.Suppression.ShouldBe("NotificationSuppressedFocused");
        focused.OperatingSystemSummary.ShouldBeNull();

        var quiet = policy.Publish(request, windowFocused: false, doNotDisturb: true);
        quiet.DeliverToOperatingSystem.ShouldBeFalse();
        quiet.Suppression.ShouldBe("NotificationDnd");

        var titleOnly = policy.Publish(request, windowFocused: false, doNotDisturb: false);
        titleOnly.DeliverToOperatingSystem.ShouldBeTrue();
        titleOnly.OperatingSystemTitle.ShouldBe("Approval needed");
        titleOnly.OperatingSystemSummary.ShouldBeNull();

        var revealed = policy.Publish(
            new NotificationRequest(NotificationKind.Failed, "Run failed", "The tool exited", "session-2"),
            windowFocused: false,
            doNotDisturb: false,
            showContent: true);
        revealed.OperatingSystemSummary.ShouldBe("The tool exited");
        policy.InApp.Count.ShouldBe(4);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }
}
