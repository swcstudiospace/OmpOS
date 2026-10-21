using OmpOS.Harness.Abstractions;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Harness session factory that only needs to be registered so startup composition can resolve it.
/// Creating a session throws, so any accidental use fails loudly.
/// </summary>
internal sealed class StubHarnessSessionFactory : IHarnessSessionFactory
{
    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt) =>
        throw new NotSupportedException("StubHarnessSessionFactory is a registration-only double; it must not create sessions.");
}
