#pragma warning disable MA0048 // The recorded call belongs with the fake runner.

namespace Autonogrammer.Harness.Notifications.Tests.Support;

/// <summary>Records argument vectors and returns a scripted exit code. It never starts a process.</summary>
internal sealed class FakeNotificationProcessRunner : INotificationProcessRunner
{
    private readonly List<NotificationProcessCall> _calls = [];

    /// <summary>Gets or sets the exit code to return. Null means the executable is missing.</summary>
    public int? ExitCode { get; set; }

    /// <summary>Gets the calls, in order.</summary>
    public IReadOnlyList<NotificationProcessCall> Calls => _calls;

    /// <inheritdoc />
    public Task<int?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        CancellationToken cancellationToken)
    {
        _calls.Add(new NotificationProcessCall(fileName, [.. arguments], standardInput.ToArray()));
        return Task.FromResult(ExitCode);
    }
}

/// <summary>One recorded call. The arguments are a vector, not a shell string.</summary>
/// <param name="FileName">The executable.</param>
/// <param name="Arguments">The argument vector.</param>
/// <param name="StandardInput">The bytes written to standard input.</param>
internal sealed record NotificationProcessCall(string FileName, IReadOnlyList<string> Arguments, IReadOnlyList<byte> StandardInput);
