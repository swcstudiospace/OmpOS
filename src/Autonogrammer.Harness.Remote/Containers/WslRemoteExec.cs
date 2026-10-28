using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Runs short POSIX shell scripts inside one WSL distribution through <c>wsl.exe --exec sh -c</c>, with the hardening of <see cref="WslCommandBuilder"/> (no local
/// shell, one script line, no secret on a command line). It is the transport of the file system and of the convention discovery of a WSL environment. At most
/// four wsl.exe processes run at once. wsl.exe exists on Windows only; elsewhere a run is a result with a start failure.
/// </summary>
public sealed class WslRemoteExec : IRemoteExec
{
    private const int MaxConcurrentCalls = 4;
    private const int EnvironmentFailureExitCode = 255;

    private readonly WslProfile _profile;
    private readonly WslClientOptions _options;
    private readonly WslCommandBuilder _builder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _slots = new(MaxConcurrentCalls, MaxConcurrentCalls);

    /// <summary>Initializes a new instance of the <see cref="WslRemoteExec"/> class.</summary>
    /// <param name="profile">The distribution. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="options">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The profile or the client options are not valid.</exception>
    public WslRemoteExec(WslProfile profile, WslClientOptions? options = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);
        _profile = profile;
        _options = options ?? new WslClientOptions();
        _builder = new WslCommandBuilder(_options);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets the distribution the scripts run in.</summary>
    public WslProfile Profile => _profile;

    /// <inheritdoc />
    public string WorkspaceRoot => _profile.WorkspacePath;

    /// <inheritdoc />
    public async Task<RemoteExecResult> RunAsync(string script, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutputBytes);
        var invocation = _builder.BuildProbe(_profile, script);
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await RemoteExec.RunInvocationAsync(invocation, input, maxOutputBytes, _options.CommandTimeout, _clock, cancellationToken).ConfigureAwait(false);
            return IsWslFailure(result) ? result with { ExitCode = EnvironmentFailureExitCode } : result;
        }
        finally
        {
            _ = _slots.Release();
        }
    }

    /// <inheritdoc />
    public Task<string?> ResolveHomeDirectoryAsync(CancellationToken cancellationToken) => ExecHome.ResolveAsync(this, cancellationToken);

    /// <summary>Releases the lock that limits the concurrent wsl.exe processes.</summary>
    public void Dispose() => _slots.Dispose();

    // wsl.exe reports its own failures (no such distribution, no such user) with an exit code that is not 255; the shared layers read 255 as "the environment failed".
    private static bool IsWslFailure(RemoteExecResult result) =>
        result.ExitCode != 0
        && !result.TimedOut
        && result.StartFailure is null
        && ContainerDiagnosisCatalog.FromWslFailure(result.ExitCode, result.Error) is { Kind: RemoteDiagnosisKind.WslDistroMissing or RemoteDiagnosisKind.WslUserMissing or RemoteDiagnosisKind.WslMissing };
}
