namespace Autonogrammer.Harness.OmpConfig;

/// <summary>One row of <c>omp models --json</c>, as observed against omp 18.12.6. Missing members stay null.</summary>
internal sealed class OmpModelRow
{
    /// <summary>Gets or sets the provider id.</summary>
    public string? Provider { get; set; }

    /// <summary>Gets or sets the catalog kind.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the model id.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the <c>provider/id</c> selector.</summary>
    public string? Selector { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the context window in tokens.</summary>
    public long? ContextWindow { get; set; }

    /// <summary>Gets or sets the most output tokens.</summary>
    public long? MaxTokens { get; set; }

    /// <summary>Gets or sets a value indicating whether the model reasons.</summary>
    public bool? Reasoning { get; set; }

    /// <summary>Gets or sets the accepted thinking levels, or null.</summary>
    public List<string>? Thinking { get; set; }

    /// <summary>Gets or sets the accepted input kinds.</summary>
    public List<string>? Input { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public OmpModelRowCost? Cost { get; set; }

    /// <summary>Gets or sets the pricing classification.</summary>
    public string? PricingStatus { get; set; }
}
