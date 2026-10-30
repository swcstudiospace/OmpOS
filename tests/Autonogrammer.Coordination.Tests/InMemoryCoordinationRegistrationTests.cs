using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>The in-memory registration exposes one singleton per coordination interface.</summary>
public sealed class InMemoryCoordinationRegistrationTests
{
    /// <summary>Each interface resolves to its in-memory implementation, and resolves to the same instance every time.</summary>
    [Fact]
    public void AddInMemoryCoordinationRegistersSingletonsForEachInterface()
    {
        using var provider = new ServiceCollection().AddInMemoryCoordination().BuildServiceProvider();

        provider.GetRequiredService<ISessionRunLeases>().ShouldBeOfType<InMemorySessionRunLeases>();
        provider.GetRequiredService<ISessionSignals>().ShouldBeOfType<InMemorySessionSignals>();
        provider.GetRequiredService<IIdempotencyStore>().ShouldBeOfType<InMemoryIdempotencyStore>();
        provider.GetRequiredService<IDispatchSignals>().ShouldBeOfType<InMemoryDispatchSignals>();
        provider.GetRequiredService<IDispatchPresence>().ShouldBeOfType<InMemoryDispatchPresence>();
        provider.GetRequiredService<IDispatchAuthNonces>().ShouldBeOfType<InMemoryDispatchAuthNonces>();
        provider.GetRequiredService<IDispatchSignals>().ShouldBeSameAs(provider.GetRequiredService<IDispatchSignals>());
        provider.GetRequiredService<IDispatchPresence>().ShouldBeSameAs(provider.GetRequiredService<IDispatchPresence>());
        provider.GetRequiredService<IDispatchAuthNonces>().ShouldBeSameAs(provider.GetRequiredService<IDispatchAuthNonces>());
        provider.GetRequiredService<ISessionSignals>().ShouldBeSameAs(provider.GetRequiredService<ISessionSignals>());
    }
}
