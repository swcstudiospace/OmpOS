using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.AgentWorker.Security;

/// <summary>
/// The public keys a worker token signature is verified against, parsed from PEM. The set owns the underlying key objects and must
/// outlive every validation that uses <see cref="Keys"/>. Private keys and RSA keys under 2048 bits are refused.
/// </summary>
internal sealed class WorkerVerificationKeySet : IDisposable
{
    private const int MinimumRsaBits = 2048;

    private readonly List<IDisposable> _owned;

    private WorkerVerificationKeySet(List<SecurityKey> keys, List<IDisposable> owned)
    {
        Keys = keys;
        _owned = owned;
    }

    /// <summary>Gets the verification keys, in configuration order.</summary>
    public IReadOnlyList<SecurityKey> Keys { get; }

    /// <summary>Parses every PEM public key.</summary>
    /// <param name="pems">The PEM texts. A literal <c>\n</c> sequence in a single-line value is read as a newline.</param>
    /// <returns>The key set. The caller disposes it.</returns>
    /// <exception cref="FormatException">The list is empty, or a value is not a usable public key. The message names the index, never the value.</exception>
    public static WorkerVerificationKeySet Parse(IReadOnlyList<string> pems)
    {
        ArgumentNullException.ThrowIfNull(pems);
        if (pems.Count == 0)
        {
            throw new FormatException("At least one public key is required.");
        }

        var keys = new List<SecurityKey>(pems.Count);
        var owned = new List<IDisposable>(pems.Count);
        try
        {
            for (var index = 0; index < pems.Count; index++)
            {
                AddKey(pems[index], index, keys, owned);
            }
        }
        catch (FormatException)
        {
            foreach (var handle in owned)
            {
                handle.Dispose();
            }

            throw;
        }

        return new WorkerVerificationKeySet(keys, owned);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var handle in _owned)
        {
            handle.Dispose();
        }
    }

    private static void AddKey(string? value, int index, List<SecurityKey> keys, List<IDisposable> owned)
    {
        var name = "Public key " + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException(name + " is blank.");
        }

        var pem = value.Contains('\n', StringComparison.Ordinal) ? value : value.Replace("\\n", "\n", StringComparison.Ordinal);
        if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            throw new FormatException(name + " is a private key; the worker accepts public keys only.");
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
            keys.Add(new ECDsaSecurityKey(ecdsa));
            owned.Add(ecdsa);
            return;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            ecdsa.Dispose();
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < MinimumRsaBits)
            {
                throw new FormatException(name + " is an RSA key under 2048 bits.");
            }

            keys.Add(new RsaSecurityKey(rsa));
            owned.Add(rsa);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
            throw new FormatException(name + " is not a PEM public key (ECDSA or RSA).", ex);
        }
        catch (FormatException)
        {
            rsa.Dispose();
            throw;
        }
    }
}
