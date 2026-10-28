namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>How a <see cref="FakeWslHost"/> behaves.</summary>
internal sealed record FakeWslHostOptions
{
    /// <summary>Gets whether the fake wsl prints a banner on stdout before the command.</summary>
    public bool Banner { get; init; }

    /// <summary>Gets whether a fake omp is installed under the distribution's <c>~/.bun/bin</c>.</summary>
    public bool InstallOmp { get; init; } = true;

    /// <summary>Gets the version the fake omp prints.</summary>
    public string OmpVersion { get; init; } = "18.12.6";

    /// <summary>Gets the text a fake <c>uname -sm</c> prints, or <see langword="null"/> to use the real one.</summary>
    public string? UnameOutput { get; init; } = "Linux x86_64";

    /// <summary>Gets the users that exist in the distribution, or <see langword="null"/> when every user exists.</summary>
    public IReadOnlyList<string>? Users { get; init; } = ["dev"];

    /// <summary>Gets extra environment variables the wrapper sets for the fake wsl and the fake omp.</summary>
    public IReadOnlyDictionary<string, string> ExtraEnvironment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
