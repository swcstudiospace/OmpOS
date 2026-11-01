using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Composition.Notifications;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>The in-app bridge records run and approval events and suppresses OS delivery while focused.</summary>
public sealed class NotificationBridgeTests
{
    [Fact]
    public void FocusSuppressesOsDeliveryAndKeepsTheInAppItem()
    {
        var bridge = new NotificationBridge(windowFocused: true);
        bridge.Observe(new CompletedEvent("end_turn"), "s1");
        bridge.Observe(new PermissionRequestEvent("c", "edit", "{}"), "s1");
        bridge.SuppressOsDelivery.ShouldBeTrue();
        bridge.Items.Count.ShouldBe(2);
        bridge.Items[0].Kind.ShouldBe("run-finished");
        bridge.Items[0].ShowContent.ShouldBeFalse();
    }

    [Fact]
    public void ScheduledTaskKindsHaveNoProducer()
    {
        var bridge = new NotificationBridge();
        bridge.Observe(new ToolCallEvent("1", "read", "{}"));
        bridge.Items.ShouldBeEmpty();
    }
}
