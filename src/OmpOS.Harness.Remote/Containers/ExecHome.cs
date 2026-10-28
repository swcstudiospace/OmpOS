using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>Reads the home directory of the user of an environment, the same way for a container and a distribution.</summary>
internal static class ExecHome
{
    private const int MaxHomeBytes = 4096;

    /// <summary>Runs <c>printf %s "$HOME"</c> in the environment.</summary>
    /// <param name="exec">The environment.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The absolute home directory, or <see langword="null"/> when it could not be read or is not an absolute path.</returns>
    internal static async Task<string?> ResolveAsync(IRemoteExec exec, CancellationToken cancellationToken)
    {
        var result = await exec.RunAsync("printf %s \"$HOME\"", ReadOnlyMemory<byte>.Empty, MaxHomeBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        var home = result.OutputText().Trim();
        return SshProfileRules.IsValidAbsoluteRemotePath(home) ? home : null;
    }
}
