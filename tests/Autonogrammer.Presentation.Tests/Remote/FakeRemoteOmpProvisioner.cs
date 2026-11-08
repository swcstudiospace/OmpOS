using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// An <see cref="IRemoteOmpProvisioner"/> that answers what a test queued, records every call, throws what a test set, and can hold an install until the
/// test releases it through <see cref="InstallGate"/>.
/// </summary>
internal sealed class FakeRemoteOmpProvisioner : IRemoteOmpProvisioner
{
    private readonly Queue<RemoteOmpReport> _checks = new();
    private readonly Queue<RemoteOmpReport> _installs = new();

    /// <summary>Gets the environment identifiers the checks were made for, in order.</summary>
    public List<string> CheckRequests { get; } = [];

    /// <summary>Gets each install request: the environment and the lines the page passed.</summary>
    public List<(string EnvironmentId, IReadOnlyList<string> Lines)> InstallRequests { get; } = [];

    /// <summary>Gets or sets the exception every later call throws instead of answering, or <see langword="null"/>.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets or sets the source the next install answers from, or <see langword="null"/> to answer from the queue at once.</summary>
    public TaskCompletionSource<RemoteOmpReport>? InstallGate { get; set; }

    /// <summary>Queues the answer of the next check.</summary>
    /// <param name="report">The answer.</param>
    /// <returns>This fake, for chaining.</returns>
    public FakeRemoteOmpProvisioner ReplyToCheck(RemoteOmpReport report)
    {
        _checks.Enqueue(report);
        return this;
    }

    /// <summary>Queues the answer of the next install.</summary>
    /// <param name="report">The answer.</param>
    /// <returns>This fake, for chaining.</returns>
    public FakeRemoteOmpProvisioner ReplyToInstall(RemoteOmpReport report)
    {
        _installs.Enqueue(report);
        return this;
    }

    /// <inheritdoc />
    public Task<RemoteOmpReport> CheckAsync(string environmentId, CancellationToken cancellationToken)
    {
        CheckRequests.Add(environmentId);
        return Failure is { } failure ? Task.FromException<RemoteOmpReport>(failure) : Task.FromResult(_checks.Dequeue());
    }

    /// <inheritdoc />
    public Task<RemoteOmpReport> InstallAsync(string environmentId, IReadOnlyList<string> checkedActions, CancellationToken cancellationToken)
    {
        InstallRequests.Add((environmentId, checkedActions.ToList()));
        if (Failure is { } failure)
        {
            return Task.FromException<RemoteOmpReport>(failure);
        }

        return InstallGate is { } gate ? gate.Task : Task.FromResult(_installs.Dequeue());
    }
}
