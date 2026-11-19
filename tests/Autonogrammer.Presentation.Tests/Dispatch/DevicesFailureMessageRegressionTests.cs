using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>A host failure's message is diagnostic: the devices state shows the copy for the phase the failure maps to, never the message.</summary>
public sealed class DevicesFailureMessageRegressionTests
{
    /// <summary>The not-configured host's message is not what the devices page shows; the disabled phase copy is.</summary>
    [Fact]
    public async Task ANotConfiguredHostShowsThePhaseCopyNotItsMessage()
    {
        var host = new NotConfiguredDispatchHost();
        var failure = await Should.ThrowAsync<DispatchHostException>(() => host.ListDevicesAsync(CancellationToken.None)).ConfigureAwait(true);

        var devices = DevicesViewModel.Failed(failure);

        devices.Phase.ShouldBe(DispatchPhase.Disabled);
        devices.StatusText.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.Disabled));
        devices.StatusText.ShouldNotBe(failure.Message, StringComparer.Ordinal);
    }

    /// <summary>A diagnostic message never reaches the devices state, whatever the kind.</summary>
    [Fact]
    public void AHostMessageIsNotShownForAnyKind()
    {
        const string diagnostic = "internal-diagnostic-text";

        var devices = DevicesViewModel.Failed(new DispatchHostException(DispatchHostFailureKind.Unavailable, diagnostic));

        devices.StatusText.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.Unreachable));
        devices.StatusText.ShouldNotContain(diagnostic);
    }
}
