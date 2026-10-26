using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>A scripted <see cref="IOmpLiveSteering"/>: the capability is set by the test, every request is recorded, and the answer can be scripted.</summary>
internal sealed class FakeLiveSteering : IOmpLiveSteering
{
    /// <summary>Gets or sets a value indicating whether the fake reports live steering as supported.</summary>
    public bool Supported { get; set; } = true;

    /// <summary>Gets the requests received, in order.</summary>
    public List<(string Text, OmpSteerLane Queue)> Requests { get; } = [];

    /// <summary>Gets or sets the answer to a request. Null answers Injected on the requested queue.</summary>
    public Func<string, OmpSteerLane, Task<OmpSteerResult>>? Handler { get; set; }

    /// <inheritdoc />
    public bool IsSupported => Supported;

    /// <inheritdoc />
    public Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        Requests.Add((text, queue));
        return Handler?.Invoke(text, queue) ?? Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Injected, queue, null));
    }
}
