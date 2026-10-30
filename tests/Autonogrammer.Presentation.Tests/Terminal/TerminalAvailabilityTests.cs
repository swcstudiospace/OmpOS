using Autonogrammer.Presentation.Terminal;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Terminal;

/// <summary>The local-terminal decision per head (phase 43, VX-08): only desktop spawns a shell.</summary>
public sealed class TerminalAvailabilityTests
{
    /// <summary>A mobile head has no local terminal and says why.</summary>
    [Fact]
    public void MobileReportsLocalTerminalUnavailableWithReason()
    {
        var surface = new TerminalSurfaceModel(TerminalHostKind.Mobile, transport: null);

        surface.LocalAvailability.IsAvailable.ShouldBeFalse();
        surface.LocalAvailability.Reason.ShouldBe(TerminalAvailability.MobileReason);
        surface.LocalAvailability.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>The web head has no local terminal and says why, with a reason distinct from mobile.</summary>
    [Fact]
    public void WebReportsLocalTerminalUnavailableWithReason()
    {
        var surface = new TerminalSurfaceModel(TerminalHostKind.Web, transport: null);

        surface.LocalAvailability.IsAvailable.ShouldBeFalse();
        surface.LocalAvailability.Reason.ShouldBe(TerminalAvailability.WebReason);
        surface.LocalAvailability.Reason.ShouldNotBe(TerminalAvailability.MobileReason, StringComparer.Ordinal);
    }

    /// <summary>A desktop head runs a local terminal and gives no reason.</summary>
    [Fact]
    public void DesktopReportsLocalAvailable()
    {
        var surface = new TerminalSurfaceModel(TerminalHostKind.Desktop, transport: null);

        surface.LocalAvailability.IsAvailable.ShouldBeTrue();
        surface.LocalAvailability.Reason.ShouldBeEmpty();
    }

    /// <summary>An undefined host kind is refused instead of defaulting to a head that may spawn a shell.</summary>
    [Fact]
    public void UndefinedHostKindIsRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TerminalAvailability.ForLocal((TerminalHostKind)99));
    }

    /// <summary>A head without a transport cannot attach a remote terminal, and attaching fails loudly.</summary>
    [Fact]
    public async Task AttachWithoutTransportIsRefused()
    {
        var surface = new TerminalSurfaceModel(TerminalHostKind.Web, transport: null);

        surface.CanAttachRemote.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(
            () => surface.AttachRemoteAsync("t-1", TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }
}
