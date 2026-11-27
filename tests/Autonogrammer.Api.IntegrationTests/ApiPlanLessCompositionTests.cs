using System.Reflection;
using Autonogrammer.Api.Harness;
using Autonogrammer.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Pins the Api composition roots for Phase 82 Task 2: both factories compose directly from <c>HarnessOptions</c> and
/// accept no <see cref="OmpLaunchPlan"/> (and no planner), so Task 4's parity scope covers the Native composition
/// root only.
/// </summary>
public sealed class ApiPlanLessCompositionTests
{
    /// <summary>Neither Api factory takes an <see cref="OmpLaunchPlan"/> in any constructor or method, nor the planner type.</summary>
    [Theory]
    [InlineData(typeof(AcpHarnessSessionFactory))]
    [InlineData(typeof(OmpRpcHarnessSessionFactory))]
    public void FactoryAcceptsNoLaunchPlan(Type factoryType)
    {
        var members = factoryType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(factoryType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)))
            .Concat(factoryType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(method => method.ReturnType));

        members.ShouldNotContain(typeof(OmpLaunchPlan));
        members.ShouldNotContain(typeof(OmpLaunchPlanner));
    }
}
