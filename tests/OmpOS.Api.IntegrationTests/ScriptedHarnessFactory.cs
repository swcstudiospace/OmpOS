using OmpOS.Api.Harness;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// An <see cref="IHarnessSessionFactory"/> replaying one fixed <see cref="InMemoryScript"/>
/// for every prompt. Tests build scripts with exact event counts for resume assertions.
/// </summary>
internal sealed class ScriptedHarnessFactory : IHarnessSessionFactory
{
    private readonly InMemoryScript _script;

    /// <summary>Initializes a new instance of the <see cref="ScriptedHarnessFactory"/> class.</summary>
    /// <param name="script">The script every prompt replays.</param>
    public ScriptedHarnessFactory(InMemoryScript script)
    {
        _script = script;
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new InMemoryOmpAdapter(_script).CreateSession();
    }

    /// <summary>Builds a script of <paramref name="deltas"/> text deltas plus a terminal completion.</summary>
    /// <param name="deltas">The number of text-delta events.</param>
    /// <returns>A script emitting exactly <paramref name="deltas"/> + 1 events.</returns>
    public static InMemoryScript DeltaScript(int deltas)
    {
        var steps = new List<InMemoryStep>(deltas + 1);
        for (var i = 0; i < deltas; i++)
        {
            steps.Add(new EmitStep(new TextDeltaEvent($"delta-{i}")));
        }

        steps.Add(new EmitStep(new CompletedEvent("end_turn")));
        return new InMemoryScript { Steps = steps };
    }
}
