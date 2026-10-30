namespace Autonogrammer.Sessions.Tests;

/// <summary>Runs the session repository contract against <see cref="InMemorySessionRepository"/>.</summary>
public sealed class InMemoryRepositoryFixture : SessionRepositoryContractTests<InMemorySessionRepository>
{
    /// <inheritdoc />
    protected override Task<InMemorySessionRepository> CreateRepositoryAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new InMemorySessionRepository());
}
