using System.Buffers;
using System.Text.Json;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Asks omp whether a provider has a credential and hands omp an API key to store. Autonogrammer keeps no copy of any key: the key
/// travels once, as JSON on the standard input of <c>omp auth import -</c>, never on a command line, and omp stores it.
/// There is no removal: omp has no headless way to delete a credential, so none is offered here.
/// </summary>
public sealed class OmpCredentialStore
{
    /// <summary>The sentence the settings view shows next to a key field. It states where the key goes and how it is protected.</summary>
    public const string StorageNote =
        "omp keeps this key in its own credential database (agent.db) with owner-only permissions, unencrypted; Autonogrammer keeps no copy.";

    private const int ExitCodePresent = 0;
    private const int ExitCodeAbsent = 1;

    private readonly OmpCommandRunner _runner;

    /// <summary>Creates a credential store.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public OmpCredentialStore(OmpConfigOptions options)
        : this(new OmpCommandRunner(options))
    {
    }

    /// <summary>Creates a credential store that shares a runner.</summary>
    /// <param name="runner">The runner that starts omp.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> is null.</exception>
    public OmpCredentialStore(OmpCommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    /// <summary>
    /// Asks omp whether it resolves a credential for <paramref name="providerId"/> by running <c>omp token &lt;provider&gt;</c>:
    /// exit 0 is <see cref="OmpCredentialPresence.Present"/>, exit 1 is <see cref="OmpCredentialPresence.Absent"/>, anything else
    /// or a timeout is <see cref="OmpCredentialPresence.Unknown"/>. The printed token is read and discarded at once; it is never
    /// stored, logged or returned.
    /// </summary>
    /// <remarks>
    /// omp answers for any source of credential, so an environment variable counts. For a <c>models.yml</c> provider whose
    /// <c>apiKey</c> names an environment variable, omp answers Present even when that variable is unset (it then falls back to
    /// the variable's name as a literal key; verified on omp 18.12.6), so Present is not proof of a usable key for such providers.
    /// </remarks>
    /// <param name="providerId">The provider id; it must match <see cref="OmpProviderId.IsValid"/>.</param>
    /// <param name="cancellationToken">Cancels the check; a cancelled check throws.</param>
    /// <returns>The presence, <see cref="OmpCredentialPresence.Unknown"/> for an invalid id without starting omp.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpCredentialPresence> GetPresenceAsync(string providerId, CancellationToken cancellationToken)
    {
        if (!OmpProviderId.IsValid(providerId))
        {
            return OmpCredentialPresence.Unknown;
        }

        var result = await _runner.RunAsync(
            new OmpCommandRequest(["token", providerId]) { CaptureStandardOutput = false },
            cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null || result.TimedOut)
        {
            return OmpCredentialPresence.Unknown;
        }

        return result.ExitCode switch
        {
            ExitCodePresent => OmpCredentialPresence.Present,
            ExitCodeAbsent => OmpCredentialPresence.Absent,
            _ => OmpCredentialPresence.Unknown,
        };
    }

    /// <summary>
    /// Imports <paramref name="key"/> as the API key of <paramref name="providerId"/> through <c>omp auth import -</c> and then
    /// verifies with <see cref="GetPresenceAsync"/> that omp resolves a credential. The JSON document is built into a byte buffer
    /// that is cleared as soon as it was written. The message of the result never contains the key. See <see cref="StorageNote"/>.
    /// </summary>
    /// <param name="providerId">The provider id; it must match <see cref="OmpProviderId.IsValid"/>, otherwise nothing is started.</param>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">Cancels the import; a cancelled import throws.</param>
    /// <returns>Whether the key was imported and verified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpCredentialImportResult> ImportApiKeyAsync(string providerId, SecretString key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!OmpProviderId.IsValid(providerId))
        {
            return Failed("The provider id is not valid: use lower-case letters, digits, '.', '_' and '-', at most 64 characters.");
        }

        var buffer = new ArrayBufferWriter<byte>();
        OmpCommandResult result;
        try
        {
            WriteDocument(buffer, providerId, key);
            result = await _runner.RunAsync(
                new OmpCommandRequest(["auth", "import", "-"])
                {
                    StandardInput = buffer.WrittenMemory,
                    CaptureStandardOutput = false,
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // ArrayBufferWriter<T>.Clear zeroes the bytes written so far, so the key does not outlive the call in this buffer.
            buffer.Clear();
        }

        if (result.StartFailure is not null)
        {
            return Failed("omp could not be started, so the key was not imported.");
        }

        if (result.TimedOut)
        {
            return Failed("omp did not finish importing the key within the time limit.");
        }

        if (result.ExitCode != 0)
        {
            return Failed($"omp refused the key import (exit code {result.ExitCode}).");
        }

        var presence = await GetPresenceAsync(providerId, cancellationToken).ConfigureAwait(false);
        return presence == OmpCredentialPresence.Present
            ? new OmpCredentialImportResult(OmpCredentialImportStatus.Imported, $"The key for {providerId} was stored by omp.")
            : Failed($"omp accepted the import but does not report a credential for {providerId}.");
    }

    private static OmpCredentialImportResult Failed(string message) =>
        new(OmpCredentialImportStatus.Failed, message);

    private static void WriteDocument(ArrayBufferWriter<byte> buffer, string providerId, SecretString key)
    {
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        writer.WriteStartArray("credentials");
        writer.WriteStartObject();
        writer.WriteString("provider", providerId);
        writer.WriteStartObject("credential");
        writer.WriteString("type", "api_key");
        writer.WriteString("key", key.Reveal());
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}
