namespace OmpOS.Native.Settings.Models;

/// <summary>The sentences of a refused catalogue add. None of them quotes a key, a path or a model reply.</summary>
internal static class CatalogueAddCopy
{
    /// <summary>The listing id names no catalogue row.</summary>
    public const string UnknownListing = "This provider is not in the catalogue.";

    /// <summary>The route id names no route of the row.</summary>
    public const string UnknownRoute = "This provider has no such route.";

    /// <summary>The coding plan rule was not confirmed.</summary>
    public const string PlanRuleNotAccepted = "Confirm that you have read the vendor's rule text for this plan before you add it.";

    /// <summary>The route takes a key and none was given.</summary>
    public const string KeyMissing = "Enter an API key for this provider.";

    /// <summary>The key store refused the key.</summary>
    public const string KeyNotStored = "OmpOS could not store the key. Nothing was changed.";

    /// <summary>Another block already uses the key variable the id would get.</summary>
    public const string KeyVariableTaken = "Another provider already uses the key variable this one would get. Remove that provider first.";

    /// <summary>The id belongs to a provider OmpOS does not own.</summary>
    public const string NotOwned = "A provider with this id already exists in models.yml that OmpOS does not own. Remove it by hand, then add this one.";

    /// <summary>omp could not be run to check or write the change.</summary>
    public const string OmpUnavailable = "OmpOS could not ask omp to check the change, so nothing was changed.";

    /// <summary>Returns the sentence for a sign-in row, which has no key and no block.</summary>
    /// <param name="loginCommand">The omp command that signs in, or null.</param>
    /// <returns>The sentence.</returns>
    public static string SignInOnly(string? loginCommand) =>
        string.IsNullOrWhiteSpace(loginCommand)
            ? "This provider signs in through omp, not with a key. Sign in with omp."
            : $"This provider signs in through omp, not with a key. In omp, run {loginCommand}.";
}
