namespace OmpOS.Harness.Plugins;

/// <summary>
/// An inclusive range of plugin API versions a host supports.
/// </summary>
public sealed record PluginApiVersionRange
{
    /// <summary>
    /// Initializes a range.
    /// </summary>
    /// <param name="minimum">The lowest supported API version. It must be at least 1.</param>
    /// <param name="maximum">The highest supported API version. It must not be below <paramref name="minimum"/>.</param>
    public PluginApiVersionRange(int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimum, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, minimum);
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>
    /// The API version this build of the host implements: version 1 only.
    /// </summary>
    public static PluginApiVersionRange Current { get; } = new(1, 1);

    /// <summary>
    /// The lowest supported API version.
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// The highest supported API version.
    /// </summary>
    public int Maximum { get; }

    /// <summary>
    /// Returns true when <paramref name="apiVersion"/> is inside the range, both ends included.
    /// </summary>
    /// <param name="apiVersion">The API version a manifest declares.</param>
    public bool Contains(int apiVersion) => apiVersion >= Minimum && apiVersion <= Maximum;

    /// <summary>
    /// Describes why a manifest with an unsupported API version is rejected. The message names the plugin, the version
    /// it requires and this range.
    /// </summary>
    /// <param name="manifest">A manifest whose API version is outside this range.</param>
    /// <returns>The rejection message.</returns>
    public string DescribeRejection(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return $"Plugin '{manifest.Id}' requires API version {manifest.ApiVersion}, but this host supports API versions {this}.";
    }

    /// <inheritdoc />
    public override string ToString() => $"{Minimum} to {Maximum}";
}
