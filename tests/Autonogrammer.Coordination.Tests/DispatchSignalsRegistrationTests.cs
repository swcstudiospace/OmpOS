using Autonogrammer.Coordination.Dragonfly;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// Dragonfly registration maps the dispatch seams without opening a connection. Resolving the multiplexer would connect,
/// so these tests read the service descriptors only.
/// </summary>
public sealed class DispatchSignalsRegistrationTests
{
    /// <summary>Each dispatch interface maps to its Dragonfly implementation as a singleton.</summary>
    [Fact]
    public void AddDragonflyCoordinationMapsDispatchSeamsWithoutConnecting()
    {
        var services = new ServiceCollection();
        services.AddDragonflyCoordination("127.0.0.1:1,abortConnect=false");

        Descriptor(services, typeof(IDispatchSignals)).ImplementationType.ShouldBe(typeof(DragonflyDispatchSignals));
        Descriptor(services, typeof(IDispatchPresence)).ImplementationType.ShouldBe(typeof(DragonflyDispatchPresence));
        Descriptor(services, typeof(IDispatchAuthNonces)).ImplementationType.ShouldBe(typeof(DragonflyDispatchAuthNonces));
        Descriptor(services, typeof(IDispatchSignals)).Lifetime.ShouldBe(ServiceLifetime.Singleton);
        Descriptor(services, typeof(IDispatchPresence)).Lifetime.ShouldBe(ServiceLifetime.Singleton);
        Descriptor(services, typeof(IDispatchAuthNonces)).Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    private static ServiceDescriptor Descriptor(IServiceCollection services, Type serviceType)
    {
        return services.Single(descriptor => descriptor.ServiceType == serviceType);
    }
}
