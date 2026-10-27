using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Resolves a host alias to the configuration ssh would use, by running <c>ssh -G -- &lt;alias&gt;</c>. ssh evaluates <c>Include</c>,
/// <c>Host</c> and <c>Match</c> itself. Note that a <c>Match exec</c> line in the user's own configuration runs its command while ssh
/// evaluates the configuration; that is the user's configuration doing what the user wrote.
/// </summary>
public sealed class SshConfigResolver
{
    private readonly SshClientOptions _options;
    private readonly SshProcessRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="SshConfigResolver"/> class.</summary>
    /// <param name="options">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    public SshConfigResolver(SshClientOptions? options = null, TimeProvider? clock = null)
    {
        _options = options ?? new SshClientOptions();
        _runner = new SshProcessRunner(clock);
    }

    /// <summary>Resolves an alias.</summary>
    /// <param name="alias">The host alias. It must match <see cref="SshProfileRules.IsValidAlias"/>: it is never allowed to look like an ssh option.</param>
    /// <param name="cancellationToken">Cancels the lookup and stops ssh.</param>
    /// <returns>The configuration, or the diagnosis of why ssh produced none.</returns>
    /// <exception cref="ArgumentException">The alias is not valid.</exception>
    public async Task<SshConfigResolution> ResolveAsync(string alias, CancellationToken cancellationToken)
    {
        if (!SshProfileRules.IsValidAlias(alias))
        {
            throw new ArgumentException("The alias is not a valid ssh host alias.", nameof(alias));
        }

        var arguments = new List<string>();
        if (_options.ConfigFile is { } configFile)
        {
            arguments.Add("-F");
            arguments.Add(configFile);
        }

        arguments.Add("-G");
        arguments.Add("--");
        arguments.Add(alias);
        var result = await _runner.RunAsync(
            new SshInvocation { FileName = _options.SshPath, Arguments = arguments },
            _options.CommandTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.StartFailure is not null)
        {
            return new SshConfigResolution { Failure = RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.SshMissing, result.StartFailure) };
        }

        if (result.TimedOut || result.ExitCode != 0)
        {
            var detail = result.TimedOut ? "ssh did not finish reading its configuration in time." : RemoteDiagnosisCatalog.Excerpt(result.StandardError);
            return new SshConfigResolution { Failure = RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.Unknown, detail) };
        }

        return new SshConfigResolution { Config = SshEffectiveConfig.Parse(alias, result.StandardOutput) };
    }
}
