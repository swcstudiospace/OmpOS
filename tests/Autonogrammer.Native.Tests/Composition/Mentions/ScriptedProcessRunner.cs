using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Native.Tests.Composition.Mentions;

/// <summary>A process runner whose answer a test scripts: it records each request and calls the script.</summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly Func<ProcessRequest, CancellationToken, Task<ProcessOutcome>> _script;

    /// <summary>Creates the runner.</summary>
    /// <param name="script">Answers each request.</param>
    internal ScriptedProcessRunner(Func<ProcessRequest, CancellationToken, Task<ProcessOutcome>> script)
    {
        _script = script;
    }

    /// <summary>Gets the requests received so far, oldest first.</summary>
    internal List<ProcessRequest> Requests { get; } = [];

    /// <inheritdoc />
    public Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _script(request, cancellationToken);
    }
}
