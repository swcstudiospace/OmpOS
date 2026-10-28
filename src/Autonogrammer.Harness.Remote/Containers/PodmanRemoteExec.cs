using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Runs short POSIX shell scripts inside one podman container through <c>podman exec -i</c>, with the hardening of <see cref="PodmanExecCommandBuilder"/> (no local
/// shell, one script line, no secret on a command line). It is the transport of the file system and of the convention discovery of a container environment. At
/// most four podman processes run at once. A podman failure (exit code 125) is reported as exit code 255, which the shared layers read as "the environment itself
/// failed" exactly as they read an ssh failure; the stderr text keeps podman's own words.
/// </summary>
public sealed class PodmanRemoteExec : IRemoteExec
{
    private const int MaxConcurrentCalls = 4;
    private const int PodmanFailureExitCode = 125;
    private const int EnvironmentFailureExitCode = 255;

    private readonly ContainerProfile _profile;
    private readonly PodmanClientOptions _options;
    private readonly PodmanExecCommandBuilder _builder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _slots = new(MaxConcurrentCalls, MaxConcurrentCalls);

    /// <summary>Initializes a new instance of the <see cref="PodmanRemoteExec"/> class.</summary>
    /// <param name="profile">The container. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="options">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The profile or the client options are not valid.</exception>
    public PodmanRemoteExec(ContainerProfile profile, PodmanClientOptions? options = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);
        _profile = profile;
        _options = options ?? new PodmanClientOptions();
        _builder = new PodmanExecCommandBuilder(_options);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets the container the scripts run in.</summary>
    public ContainerProfile Profile => _profile;

    /// <inheritdoc />
    public string WorkspaceRoot => _profile.WorkspaceFolder;

    /// <inheritdoc />
    public async Task<RemoteExecResult> RunAsync(string script, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutputBytes);
        var invocation = _builder.BuildProbe(_profile, script);
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await RemoteExec.RunInvocationAsync(invocation, input, maxOutputBytes, _options.CommandTimeout, _clock, cancellationToken).ConfigureAwait(false);
            return result.ExitCode == PodmanFailureExitCode ? result with { ExitCode = EnvironmentFailureExitCode } : result;
        }
        finally
        {
            _ = _slots.Release();
        }
    }

    /// <inheritdoc />
    public Task<string?> ResolveHomeDirectoryAsync(CancellationToken cancellationToken) => ExecHome.ResolveAsync(this, cancellationToken);

    /// <summary>Releases the lock that limits the concurrent podman processes.</summary>
    public void Dispose() => _slots.Dispose();
}
