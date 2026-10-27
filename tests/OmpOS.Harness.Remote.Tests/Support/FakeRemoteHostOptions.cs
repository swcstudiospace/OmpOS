namespace OmpOS.Harness.Remote.Tests;

/// <summary>How a <see cref="FakeRemoteHost"/> behaves.</summary>
internal sealed record FakeRemoteHostOptions
{
    /// <summary>Gets the failure mode of the fake ssh (see the header of <c>fake-ssh</c>); empty is a healthy machine.</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Gets whether the fake ssh prints a login banner on stdout before the remote command.</summary>
    public bool Banner { get; init; }

    /// <summary>Gets whether a fake omp is installed under the remote <c>~/.bun/bin</c>.</summary>
    public bool InstallOmp { get; init; } = true;

    /// <summary>Gets the version the fake omp prints.</summary>
    public string OmpVersion { get; init; } = "18.12.6";

    /// <summary>Gets whether the fake omp prints a non-JSON line right after its first frame.</summary>
    public bool NoiseAfterHandshake { get; init; }

    /// <summary>Gets the text a fake <c>uname -sm</c> prints, or <see langword="null"/> to use the real one.</summary>
    public string? UnameOutput { get; init; } = "Linux x86_64";

    /// <summary>Gets the canned <c>ssh -G</c> outputs by alias.</summary>
    public IReadOnlyDictionary<string, string> CannedConfigs { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets extra environment variables the wrapper sets for the fake ssh and the fake omp.</summary>
    public IReadOnlyDictionary<string, string> ExtraEnvironment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
