using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Contract tests for FakeSandboxProvider.
/// </summary>
public sealed class FakeSandboxProviderContractTests : SandboxProviderContractTests<FakeSandboxProvider>
{
    /// <inheritdoc />
    protected override FakeSandboxProvider CreateProvider() => new();

    /// <inheritdoc />
    protected override void DisposeProvider(FakeSandboxProvider provider) => provider.Clear();

    /// <inheritdoc />
    protected override ProvisionRequest ArrangeProvisionTimeout(FakeSandboxProvider provider, ProvisionRequest request)
    {
        // The fake has no clock: a timeout that is not positive is its expired deadline.
        ArgumentNullException.ThrowIfNull(request);
        return request with { Timeout = TimeSpan.Zero };
    }

    [Fact]
    public async Task FailProvision_InjectsFailures()
    {
        var provider = CreateProvider();
        try
        {
            provider.FailProvision(2);

            var request = CreateProvisionRequest();

            await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request));
            await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request));

            var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);
            result.NewlyProvisioned.ShouldBeTrue();
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task FailTeardown_InjectsFailures()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            provider.FailTeardown(2);

            var request = CreateProvisionRequest();
            var result = await provider.ProvisionAsync(request, ct);

            await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync(result.Descriptor.Id));
            await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync(result.Descriptor.Id));

            await provider.TeardownAsync(result.Descriptor.Id, ct);
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task FailAfterPartial_LeavesNoRunningSandbox()
    {
        var provider = CreateProvider();
        try
        {
            provider.FailAfterPartial(true);

            var request = CreateProvisionRequest();
            await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }
}
