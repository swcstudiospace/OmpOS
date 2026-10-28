namespace OmpOS.Api.Services.Providers;

/// <summary>Tells whether an environment variable of the service process is set. It never returns a value.</summary>
internal interface IProviderEnvironment
{
    /// <summary>Tells whether the variable is set to a non-empty value.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True when it is set.</returns>
    bool IsSet(string name);
}
