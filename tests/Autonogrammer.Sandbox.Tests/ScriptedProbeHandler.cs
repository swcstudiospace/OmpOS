using System.Net;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Answers worker health probes from a script: the n-th request gets the n-th step, the last step repeats.
/// A step is a status code, or an exception to throw.
/// </summary>
internal sealed class ScriptedProbeHandler : HttpMessageHandler
{
    private readonly List<Func<HttpResponseMessage>> _steps = [];

    /// <summary>Gets the URI of every request received, in order.</summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>Adds a step that answers with a status code.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <returns>This handler.</returns>
    public ScriptedProbeHandler Then(HttpStatusCode status)
    {
        _steps.Add(() => new HttpResponseMessage(status));
        return this;
    }

    /// <summary>Adds a step that fails like a refused connection.</summary>
    /// <returns>This handler.</returns>
    public ScriptedProbeHandler ThenConnectionFailure()
    {
        _steps.Add(() => throw new HttpRequestException("connection refused"));
        return this;
    }

    /// <summary>Adds a step that fails like a per-request timeout.</summary>
    /// <returns>This handler.</returns>
    public ScriptedProbeHandler ThenTimeout()
    {
        _steps.Add(() => throw new TaskCanceledException("the request timed out"));
        return this;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = Requests.Count;
        Requests.Add(request.RequestUri!);
        return Task.FromResult(_steps[Math.Min(index, _steps.Count - 1)]());
    }
}
