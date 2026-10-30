namespace Autonogrammer.Cowork.Chat;

/// <summary>A composer-supplied environment variable is not a provider key, so it is refused.</summary>
public sealed class CoworkEnvironmentRefusedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CoworkEnvironmentRefusedException"/> class.</summary>
    public CoworkEnvironmentRefusedException()
        : this("name")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkEnvironmentRefusedException"/> class.</summary>
    /// <param name="name">The refused variable name. The value is not stored.</param>
    public CoworkEnvironmentRefusedException(string name)
        : base($"The Cowork environment refuses '{name}'. Only AUTONOGRAMMER_PROVIDER_*_KEY variables are kept.")
    {
        Name = name;
    }

    /// <summary>Initializes a new instance of the <see cref="CoworkEnvironmentRefusedException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public CoworkEnvironmentRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Name = string.Empty;
    }

    /// <summary>Gets the refused variable name.</summary>
    public string Name { get; }
}
