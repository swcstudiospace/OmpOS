namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The <c>cost</c> object of a model row.</summary>
internal sealed class OmpModelRowCost
{
    /// <summary>Gets or sets the input token price.</summary>
    public double Input { get; set; }

    /// <summary>Gets or sets the output token price.</summary>
    public double Output { get; set; }

    /// <summary>Gets or sets the cache read price.</summary>
    public double CacheRead { get; set; }

    /// <summary>Gets or sets the cache write price.</summary>
    public double CacheWrite { get; set; }
}
