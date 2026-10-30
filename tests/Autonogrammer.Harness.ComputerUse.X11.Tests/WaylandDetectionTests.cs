using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.X11.Tests;

/// <summary>Wayland and missing-display classification without opening the session display.</summary>
public sealed class WaylandDetectionTests
{
    [Fact]
    public void SessionTypeWaylandIsWayland()
    {
        WaylandDetection.IsWayland(name => Same(name, "XDG_SESSION_TYPE") ? "wayland" : null).ShouldBeTrue();
        ComputerAvailability availability = WaylandDetection.Classify(
            name => Same(name, "XDG_SESSION_TYPE") ? "wayland" : Same(name, "DISPLAY") ? ":0" : null,
            displayOpenSucceeded: true);
        availability.ShouldBe(ComputerAvailability.WaylandUnsupported);
        ComputerAvailabilityRemediation.For(availability).ShouldBe("computer.unavailable.wayland");
        WaylandDetection.WaylandMessage.ShouldContain("Xorg (X11) session");
    }

    [Fact]
    public void WaylandDisplayIsWaylandEvenWhenDisplayPointsAtXWayland()
    {
        Func<string, string?> read = name => name switch
        {
            "WAYLAND_DISPLAY" => "wayland-0",
            "DISPLAY" => ":0",
            _ => null,
        };

        WaylandDetection.IsWayland(read).ShouldBeTrue();
        WaylandDetection.Classify(read, displayOpenSucceeded: true).ShouldBe(ComputerAvailability.WaylandUnsupported);
    }

    [Fact]
    public void PlainDisplaySessionIsNotWayland()
    {
        Func<string, string?> read = name => Same(name, "DISPLAY") ? ":1" : null;

        WaylandDetection.IsWayland(read).ShouldBeFalse();
        WaylandDetection.Classify(read, displayOpenSucceeded: true).ShouldBe(ComputerAvailability.Available);
    }

    [Fact]
    public void MissingDisplayOrFailedOpenIsNoDisplay()
    {
        Func<string, string?> unset = _ => null;
        WaylandDetection.Classify(unset, displayOpenSucceeded: false).ShouldBe(ComputerAvailability.NoDisplay);
        ComputerAvailabilityRemediation.For(ComputerAvailability.NoDisplay).ShouldBe("computer.unavailable.display");

        Func<string, string?> present = name => Same(name, "DISPLAY") ? ":1" : null;
        ComputerAvailability failed = WaylandDetection.Classify(present, displayOpenSucceeded: false);
        failed.ShouldBe(ComputerAvailability.NoDisplay);
        ComputerAvailabilityRemediation.For(failed).ShouldBe(ComputerAvailabilityRemediation.NoDisplay);
    }

    private static bool Same(string name, string expected) => string.Equals(name, expected, StringComparison.Ordinal);
}
