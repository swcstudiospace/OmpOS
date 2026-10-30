using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The harness factory of the test host for sessions that are not cloud agents'. It refuses every run, so a test that passes proves a cloud agent's run never reached the local harness.</summary>
internal sealed class LocalRunRefusedFactory : IHarnessSessionFactory
{
    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt) => throw new InvalidOperationException("A local run was requested.");
}
