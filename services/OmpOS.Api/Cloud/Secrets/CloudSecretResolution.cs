namespace OmpOS.Api.Cloud;

/// <summary>The result of resolving an agent's secret references: the values found, and the names no provider had.</summary>
internal sealed class CloudSecretResolution
{
    /// <summary>Initializes a new instance of the <see cref="CloudSecretResolution"/> class.</summary>
    /// <param name="values">The resolved values by name.</param>
    /// <param name="missing">The names that no provider has.</param>
    public CloudSecretResolution(IReadOnlyDictionary<string, string> values, IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(missing);
        Values = values;
        Missing = missing;
    }

    /// <summary>Gets the resolved values by name. They hold secrets: never log them.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Gets the names no provider had.</summary>
    public IReadOnlyList<string> Missing { get; }

    /// <inheritdoc />
    public override string ToString() => "CloudSecretResolution (values not shown)";
}
