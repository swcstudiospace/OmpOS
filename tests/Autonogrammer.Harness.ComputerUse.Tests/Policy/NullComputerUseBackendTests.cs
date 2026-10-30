using Autonogrammer.Harness.ComputerUse.Abstractions;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Null backend reports an unsupported platform and throws a title-free failure elsewhere.</summary>
public sealed class NullComputerUseBackendTests
{
    [Fact]
    public async Task AvailabilityIsUnsupportedPlatformAndOtherIoThrows()
    {
        NullComputerUseBackend backend = new();
        backend.Platform.ShouldBe(ComputerPlatform.Unsupported);
        backend.Capabilities.ShouldBe(ComputerBackendCapabilities.None);
        ComputerAvailability availability = await backend.GetAvailabilityAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        availability.ShouldBe(ComputerAvailability.UnsupportedPlatform);
        ComputerAvailabilityRemediation.For(availability).ShouldBe("computer.unavailable.platform");

        ComputerBackendException exception = await Should.ThrowAsync<ComputerBackendException>(
            () => backend.InjectAsync(new PointerMove { X = 0, Y = 0 }, TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
        exception.Failure.ShouldBe(ComputerFailure.Unavailable);
        exception.Message.ShouldNotContain("title");
        exception.Message.ShouldNotContain("typed");
        await Should.ThrowAsync<ComputerBackendException>(() => backend.ListWindowsAsync(TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
        await Should.ThrowAsync<ComputerBackendException>(() => backend.ReleaseAllInputAsync(TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
        await Should.ThrowAsync<ComputerBackendException>(() => backend.ReadClipboardTextAsync(TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
    }
}
