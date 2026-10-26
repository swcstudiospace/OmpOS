using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Substrate;

namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// A substrate reader whose result or failure a test chooses. It records the scopes it was asked about.
/// </summary>
internal sealed class GsdFakeSubstrateReader : ISubstrateContextReader
{
    private readonly Lock _gate = new();
    private readonly List<StageObservationScope> _scopes = [];

    /// <summary>Gets or sets what a read does.</summary>
    public Func<StageObservationScope, CancellationToken, SubstrateContextSnapshot> Respond { get; set; } =
        static (_, _) => new SubstrateContextSnapshot { SharedMemory = [], Lessons = [], Warnings = [] };

    /// <summary>Gets a copy of the scopes that were read.</summary>
    public IReadOnlyList<StageObservationScope> Scopes
    {
        get
        {
            lock (_gate)
            {
                return [.. _scopes];
            }
        }
    }

    /// <inheritdoc />
    public Task<SubstrateContextSnapshot> ReadAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _scopes.Add(scope);
        }

        return Task.FromResult(Respond(scope, cancellationToken));
    }
}
