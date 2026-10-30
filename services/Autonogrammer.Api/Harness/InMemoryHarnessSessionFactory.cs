using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// <see cref="IHarnessSessionFactory"/> backed by <see cref="InMemoryOmpAdapter"/>. Each prompt
/// replays a deterministic echo script (one text delta plus completion) so the vertical slice
/// runs with no model. Tests replace the factory registration to script richer streams.
/// </summary>
internal sealed class InMemoryHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly IOptions<HarnessOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="InMemoryHarnessSessionFactory"/> class.</summary>
    /// <param name="options">The harness options selecting the adapter.</param>
    public InMemoryHarnessSessionFactory(IOptions<HarnessOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var adapter = _options.Value.Adapter;
        if (!HarnessOptions.IsSupportedAdapter(adapter))
        {
            throw new InvalidOperationException(
                $"Unsupported Harness:Adapter '{adapter}'. Phase 2 supports only InMemory.");
        }

        var script = new InMemoryScript
        {
            Steps = [
                new EmitStep(new TextDeltaEvent($"echo: {prompt}")),
                new EmitStep(new CompletedEvent("end_turn")),
            ],
        };
        return new InMemoryOmpAdapter(script).CreateSession();
    }
}
