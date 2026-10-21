namespace OmpOS.Sessions.Tests;

/// <summary>Runs the session repository contract against <see cref="InMemorySessionRepository"/>.</summary>
public sealed class InMemoryRepositoryFixture : SessionRepositoryContractTests<InMemorySessionRepository>
{
    /// <inheritdoc />
    protected override InMemorySessionRepository CreateRepository() => new();
}
