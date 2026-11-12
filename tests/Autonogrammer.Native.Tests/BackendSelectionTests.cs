using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The windowing backend is the default platform detection unless <c>AUTONOGRAMMER_NATIVE_BACKEND</c> explicitly names Wayland on Linux.
/// An unknown value is refused rather than starting a backend nobody asked for.
/// </summary>
public sealed class BackendSelectionTests
{
    /// <summary>An unset variable keeps the default backend, so an existing install starts exactly as before.</summary>
    [Fact]
    public void AnUnsetVariableSelectsTheDefaultBackend()
    {
        Program.BackendSelection.Select(null, isLinux: true).ShouldBe(Program.NativeBackend.Default);
    }

    /// <summary>An empty or blank variable is treated as unset.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyVariableSelectsTheDefaultBackend(string value)
    {
        Program.BackendSelection.Select(value, isLinux: true).ShouldBe(Program.NativeBackend.Default);
    }

    /// <summary>The opt-in is honoured on Linux, whatever the case or surrounding space of the value.</summary>
    [Theory]
    [InlineData("wayland")]
    [InlineData("WAYLAND")]
    [InlineData(" Wayland ")]
    public void WaylandIsSelectedWhenNamedOnLinux(string value)
    {
        Program.BackendSelection.Select(value, isLinux: true).ShouldBe(Program.NativeBackend.Wayland);
    }

    /// <summary>The Wayland backend is built for Linux only, so naming it elsewhere is refused.</summary>
    [Fact]
    public void NamingWaylandOffLinuxIsRefused()
    {
        Should.Throw<InvalidOperationException>(() => Program.BackendSelection.Select("wayland", isLinux: false))
            .Message.ShouldContain("Linux only");
    }

    /// <summary>A value that names no known backend is refused, including an X11 name that the default already covers.</summary>
    [Theory]
    [InlineData("x11")]
    [InlineData("wayland-x")]
    [InlineData("wayland,x11")]
    public void AnUnknownBackendIsRefused(string value)
    {
        Should.Throw<InvalidOperationException>(() => Program.BackendSelection.Select(value, isLinux: true))
            .Message.ShouldContain("unknown backend");
    }
}
