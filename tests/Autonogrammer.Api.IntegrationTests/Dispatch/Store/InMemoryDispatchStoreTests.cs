using Autonogrammer.Api.Dispatch.Store;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Contracts.Tests.Dispatch.Store;

namespace Autonogrammer.Api.IntegrationTests.Dispatch.Store;

/// <summary>Runs the shared store contract against the in-memory store.</summary>
public sealed class InMemoryDispatchStoreTests : DispatchStoreContractTests<IDispatchStore>
{
    /// <inheritdoc />
    protected override Task<IDispatchStore> CreateStoreAsync(DispatchStoreLimits limits, TimeProvider time, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IDispatchStore>(new InMemoryDispatchStore(limits, time));
    }
}
