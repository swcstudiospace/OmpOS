using System.Text;

namespace OmpOS.Api.Services.Providers;

/// <summary>
/// The name of the environment variable that carries the key of an OmpOS block: <c>OMPOS_PROVIDER_&lt;ID&gt;_KEY</c> with every character of
/// the id that is not a letter or digit replaced by an underscore. The desktop head derives the same name for its key store, so one block
/// names one variable on every head.
/// </summary>
internal static class ProviderKeyNames
{
    private const string Prefix = "OMPOS_PROVIDER_";
    private const string Suffix = "_KEY";

    /// <summary>Returns the variable name for a provider id.</summary>
    /// <param name="providerId">The provider id.</param>
    /// <returns>The variable name.</returns>
    /// <exception cref="ArgumentException"><paramref name="providerId"/> is empty.</exception>
    public static string EnvNameFor(string providerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerId);
        var builder = new StringBuilder(Prefix.Length + providerId.Length + Suffix.Length);
        _ = builder.Append(Prefix);
        foreach (var character in providerId)
        {
            _ = builder.Append(character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? char.ToUpperInvariant(character) : '_');
        }

        return builder.Append(Suffix).ToString();
    }
}
