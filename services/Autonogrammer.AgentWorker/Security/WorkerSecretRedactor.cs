using System.Collections;
using System.Collections.Concurrent;

namespace Autonogrammer.AgentWorker.Security;

/// <summary>
/// Removes secret values from text before it is logged. The secrets are the values of the worker's own environment variables whose names
/// look like credentials (<c>KEY</c>, <c>TOKEN</c>, <c>SECRET</c>, <c>PASSWORD</c>, <c>PASSPHRASE</c>, <c>CREDENTIAL</c> or <c>PEM</c>) and the
/// bearer tokens of the connections currently attached. The environment of the worker is what <c>omp acp</c> inherits, so a line the
/// child writes to stderr may echo any of them.
/// </summary>
internal sealed class WorkerSecretRedactor
{
    /// <summary>Gets the text that replaces a secret.</summary>
    public const string Replacement = "[REDACTED]";

    private const int MinimumSecretLength = 8;

    private static readonly string[] SecretNameMarkers = ["KEY", "TOKEN", "SECRET", "PASSWORD", "PASSPHRASE", "CREDENTIAL", "PEM"];

    private readonly string[] _environmentSecrets;
    private readonly ConcurrentDictionary<Guid, string> _tracked = new();

    /// <summary>Initializes a new instance of the <see cref="WorkerSecretRedactor"/> class from the current process environment.</summary>
    public WorkerSecretRedactor()
        : this(Environment.GetEnvironmentVariables())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkerSecretRedactor"/> class from an explicit environment.</summary>
    /// <param name="environment">The environment variables to scan.</param>
    internal WorkerSecretRedactor(IDictionary environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var secrets = new List<string>();
        foreach (DictionaryEntry entry in environment)
        {
            if (entry.Key is string name
                && entry.Value is string value
                && value.Length >= MinimumSecretLength
                && SecretNameMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                secrets.Add(value);
            }
        }

        _environmentSecrets = [.. secrets.OrderByDescending(static secret => secret.Length)];
    }

    /// <summary>Starts redacting a secret until the returned handle is disposed. Secrets shorter than 8 characters are not tracked.</summary>
    /// <param name="secret">The secret value, for example a connection's bearer token.</param>
    /// <returns>The handle that stops redacting the secret.</returns>
    public IDisposable Track(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var id = Guid.NewGuid();
        if (secret.Length >= MinimumSecretLength)
        {
            _tracked[id] = secret;
        }

        return new Handle(this, id);
    }

    /// <summary>Replaces every known secret in <paramref name="text"/> with <see cref="Replacement"/>.</summary>
    /// <param name="text">The text to scrub.</param>
    /// <returns>The scrubbed text.</returns>
    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = text;
        foreach (var secret in _environmentSecrets)
        {
            result = result.Replace(secret, Replacement, StringComparison.Ordinal);
        }

        foreach (var secret in _tracked.Values)
        {
            result = result.Replace(secret, Replacement, StringComparison.Ordinal);
        }

        return result;
    }

    private sealed class Handle : IDisposable
    {
        private readonly WorkerSecretRedactor _owner;
        private readonly Guid _id;

        public Handle(WorkerSecretRedactor owner, Guid id)
        {
            _owner = owner;
            _id = id;
        }

        public void Dispose() => _owner._tracked.TryRemove(_id, out _);
    }
}
