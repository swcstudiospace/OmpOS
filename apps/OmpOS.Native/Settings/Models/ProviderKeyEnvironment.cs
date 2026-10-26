using System.Globalization;
using System.Text;
using OmpOS.Harness.Secrets;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// The API keys of the providers OmpOS wrote to omp's <c>models.yml</c>, as environment variables for the omp processes the head starts.
/// A custom provider block names the variable that holds its key (<c>apiKey: OMPOS_PROVIDER_&lt;ID&gt;_KEY</c>) and omp reads that
/// variable from its own process environment, so the key must reach every omp the head starts: the engine of a chat session and the
/// omp of a terminal session. The keys live in the OmpOS provider key store under the variable's name, never in <c>models.yml</c>, and
/// are merged into a process environment only after planning, so the planner and the plan it returns never hold one.
/// </summary>
/// <remarks>
/// A secret belongs here when its name has the shape <see cref="EnvNameFor"/> produces. The type holds the values as plain strings
/// because they leave it only as the environment of a child process; <see cref="ToString"/> and every message name variables, never
/// values. A store that cannot be read leaves the previous snapshot in force and is reported through <see cref="Problem"/>.
/// </remarks>
internal sealed class ProviderKeyEnvironment
{
    /// <summary>The start of every variable name this type owns.</summary>
    public const string NamePrefix = "OMPOS_PROVIDER_";

    /// <summary>The end of every variable name this type owns.</summary>
    public const string NameSuffix = "_KEY";

    private static readonly IReadOnlyDictionary<string, string> NoKeys = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly ISecretStore _store;
    private volatile IReadOnlyDictionary<string, string> _current = NoKeys;
    private volatile string? _problem;

    /// <summary>Creates the environment over a key store. Nothing is read until <see cref="RefreshAsync"/>.</summary>
    /// <param name="store">The store that holds the keys under their variable names.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public ProviderKeyEnvironment(ISecretStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Gets the variables of the last successful <see cref="RefreshAsync"/>: variable name to key. Empty before the first one.</summary>
    public IReadOnlyDictionary<string, string> Current => _current;

    /// <summary>Gets the plain sentence that says why the last refresh could not read the key store, or null when it could.</summary>
    public string? Problem => _problem;

    /// <summary>Returns the name of the environment variable that holds the key of a provider: <c>OMPOS_PROVIDER_&lt;ID&gt;_KEY</c> with the id in upper case and every character that is not a letter or digit as <c>_</c>.</summary>
    /// <param name="providerId">The provider id.</param>
    /// <returns>The variable name, which matches <c>[A-Z][A-Z0-9_]*</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="providerId"/> is null or empty.</exception>
    public static string EnvNameFor(string providerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerId);
        var builder = new StringBuilder(NamePrefix.Length + providerId.Length + NameSuffix.Length);
        builder.Append(NamePrefix);
        foreach (var character in providerId)
        {
            builder.Append(character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? char.ToUpperInvariant(character) : '_');
        }

        return builder.Append(NameSuffix).ToString();
    }

    /// <summary>Tells whether a secret name is one of the variable names this type owns.</summary>
    /// <param name="name">The secret name.</param>
    /// <returns>True when the name starts with <see cref="NamePrefix"/>, ends with <see cref="NameSuffix"/> and has a part between them made of <c>A-Z</c>, <c>0-9</c> and <c>_</c>.</returns>
    public static bool IsKeyName(string? name)
    {
        if (name is null || name.Length <= NamePrefix.Length + NameSuffix.Length
            || !name.StartsWith(NamePrefix, StringComparison.Ordinal) || !name.EndsWith(NameSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        return name.All(static character => character is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
    }

    /// <summary>Reads the key store and builds the variables of every stored provider key, without keeping them.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Variable name to key, ordered by name.</returns>
    /// <exception cref="SecretStoreException">The key store could not be read.</exception>
    public async Task<Dictionary<string, string>> BuildAsync(CancellationToken cancellationToken)
    {
        var names = await _store.ListNamesAsync(cancellationToken).ConfigureAwait(false);
        var keys = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names.Where(IsKeyName))
        {
            var secret = await _store.GetAsync(name, cancellationToken).ConfigureAwait(false);
            if (secret is not null)
            {
                keys[name] = secret.Reveal();
            }
        }

        return new Dictionary<string, string>(keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the key store again and replaces <see cref="Current"/>. A store that cannot be read keeps the previous snapshot and sets
    /// <see cref="Problem"/>; cancellation throws and changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the snapshot is replaced or the problem is recorded.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _current = await BuildAsync(cancellationToken).ConfigureAwait(false);
            _problem = null;
        }
        catch (SecretStoreException exception)
        {
            _problem = exception.Message;
        }
    }

    /// <summary>Returns a copy of <paramref name="environment"/> with the keys of <see cref="Current"/> added; a key replaces a variable of the same name.</summary>
    /// <param name="environment">The environment of the plan.</param>
    /// <returns>The merged environment. The argument is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public Dictionary<string, string> Merge(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var merged = new Dictionary<string, string>(environment, StringComparer.Ordinal);
        foreach (var (name, key) in _current)
        {
            merged[name] = key;
        }

        return merged;
    }

    /// <summary>
    /// Returns a view of <paramref name="environment"/> that adds the keys of <see cref="Current"/> at the moment it is read. A process that
    /// is started later therefore sees a key saved after the view was made.
    /// </summary>
    /// <param name="environment">The environment of the plan.</param>
    /// <returns>The live view. The argument is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public ProviderKeyOverlay Overlay(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new ProviderKeyOverlay(this, environment);
    }

    /// <summary>Names the variables, never a key.</summary>
    /// <returns>The number of variables and their names.</returns>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{nameof(ProviderKeyEnvironment)} [{string.Join(", ", _current.Keys)}]");
}
