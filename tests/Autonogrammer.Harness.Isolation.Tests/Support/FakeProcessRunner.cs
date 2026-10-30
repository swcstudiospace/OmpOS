namespace Autonogrammer.Harness.Isolation.Tests.Support;

/// <summary>A hand-written process runner. It never starts a process.</summary>
internal sealed class FakeProcessRunner : IIsolationProcessRunner
{
    private readonly List<(string FileName, IReadOnlyList<string> Arguments)> _calls = [];

    /// <summary>Gets the calls the probe made.</summary>
    public IReadOnlyList<(string FileName, IReadOnlyList<string> Arguments)> Calls => _calls;

    /// <summary>Gets or sets the handler. Null returns a successful empty run.</summary>
    public Func<string, IReadOnlyList<string>, CancellationToken, Task<IsolationProcessRun>>? Handler { get; set; }

    /// <inheritdoc />
    public Task<IsolationProcessRun> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _calls.Add((fileName, arguments));
        return Handler is null
            ? Task.FromResult(new IsolationProcessRun(0, string.Empty, false, false))
            : Handler(fileName, arguments, cancellationToken);
    }
}
