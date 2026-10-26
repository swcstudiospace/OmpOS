namespace OmpOS.Presentation.Composer;

/// <summary>One model the engine offers a session, as the composer's model picker lists it.</summary>
/// <param name="Value">The engine's identifier of the model, <c>provider/id</c> for omp. The picker sends it back unchanged to select the model.</param>
/// <param name="Name">The display name of the model.</param>
/// <param name="Provider">The provider the model belongs to; the picker groups by it.</param>
public sealed record ModelChoice(string Value, string Name, string Provider)
{
    /// <summary>The group of a model whose identifier names no provider.</summary>
    public const string OtherProvider = "other";

    /// <summary>Creates a choice and reads its provider from the part of <paramref name="value"/> before the first slash.</summary>
    /// <param name="value">The engine's identifier of the model.</param>
    /// <param name="name">The display name. A blank name falls back to the identifier.</param>
    /// <returns>The choice.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, empty or white space.</exception>
    public static ModelChoice Create(string value, string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var slash = value.IndexOf('/', StringComparison.Ordinal);
        var provider = slash > 0 ? value[..slash] : OtherProvider;
        return new ModelChoice(value, string.IsNullOrWhiteSpace(name) ? value : name, provider);
    }
}
