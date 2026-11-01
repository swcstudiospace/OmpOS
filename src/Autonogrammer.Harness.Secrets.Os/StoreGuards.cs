namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>Argument checks and the fixed failure sentences shared by every operating system store.</summary>
internal static class StoreGuards
{
    /// <summary>Checks the name argument the way <see cref="ISecretStore"/> specifies.</summary>
    /// <param name="name">The name to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public static void ValidateName(string name) => ArgumentException.ThrowIfNullOrWhiteSpace(name);

    /// <summary>Returns the one sentence that describes a failure of a store. It contains no name, value or native message.</summary>
    /// <param name="store">The human name of the store, for example <c>Linux Secret Service</c>.</param>
    /// <param name="failure">The classified failure.</param>
    /// <returns>A plain sentence.</returns>
    public static string Describe(string store, SecretStoreFailure failure) => failure switch
    {
        SecretStoreFailure.Unavailable => $"The {store} is not available on this device.",
        SecretStoreFailure.Locked => $"The {store} is locked.",
        SecretStoreFailure.Denied => $"The {store} refused access.",
        SecretStoreFailure.Corrupt => $"The {store} holds data that could not be read.",
        _ => $"The {store} reported an error.",
    };
}
