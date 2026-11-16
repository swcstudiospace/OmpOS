namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>A process seam that records each request and answers it from a delegate. It never starts a process.</summary>
internal sealed class FakeProcessRunner : IOmpProcessRunner
{
    private readonly Func<OmpProcessRequest, OmpProcessResult> _respond;
    private readonly List<OmpProcessRequest> _calls = [];

    /// <summary>Creates the runner.</summary>
    /// <param name="respond">Produces the result of each request.</param>
    public FakeProcessRunner(Func<OmpProcessRequest, OmpProcessResult> respond)
    {
        _respond = respond;
    }

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<OmpProcessRequest> Calls => _calls;

    /// <inheritdoc />
    public Task<OmpProcessResult> RunAsync(OmpProcessRequest request, CancellationToken cancellationToken)
    {
        _calls.Add(request);
        return Task.FromResult(_respond(request));
    }
}
