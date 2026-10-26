namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// omp's <c>modelRoles</c> setting: a map from a role (<c>default</c>, <c>smol</c>, <c>slow</c>, <c>plan</c>, ...) to a
/// <c>provider/model-id</c> selector. The default model is the selector of the <c>default</c> role.
/// </summary>
/// <param name="Roles">Every role omp reports, in omp's order. Empty when none is set or the setting could not be read.</param>
/// <param name="DefaultSelector">The selector of the <c>default</c> role, or null when that role is not set.</param>
/// <param name="Problem">Why the setting could not be read, or null when it was read. A non-null problem means the other members are not trustworthy.</param>
public sealed record OmpModelRoles(IReadOnlyDictionary<string, string> Roles, string? DefaultSelector, string? Problem)
{
    /// <summary>The name of the role that holds the default model.</summary>
    public const string DefaultRole = "default";

    /// <summary>Gets a value indicating whether the setting was read.</summary>
    public bool WasRead => Problem is null;
}
