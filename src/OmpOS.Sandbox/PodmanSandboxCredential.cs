using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OmpOS.Sandbox;

/// <summary>
/// Per-sandbox credential for the podman provider. Each sandbox gets a random value stored as a podman secret
/// that is removed at teardown. The value is staged in a private temp file that podman reads once; it never
/// appears in argv, compose text, labels, logs or exception messages.
/// </summary>
internal static class PodmanSandboxCredential
{
    /// <summary>Number of random bytes in a credential before base64url encoding.</summary>
    public const int ByteLength = 32;

    /// <summary>File name under <c>/run/secrets</c> where the sandbox reads the credential.</summary>
    public const string MountTarget = "ompos_sandbox_credential";

    /// <summary>File mode of the mounted credential inside the sandbox.</summary>
    public const string MountMode = "0400";

    /// <summary>Returns the podman secret name for a sandbox.</summary>
    /// <param name="sandboxId">The provider-generated sandbox id.</param>
    /// <returns>The secret name, <c>ompos-{sandboxId}-credential</c>.</returns>
    public static string SecretNameFor(string sandboxId) => $"ompos-{sandboxId}-credential";

    /// <summary>Generates a new credential: 32 random bytes, base64url encoded without padding.</summary>
    /// <returns>The credential text.</returns>
    public static string NewValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(ByteLength);
        try
        {
            return Base64Url.EncodeToString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Returns a fresh, unpredictable path for staging a credential file.</summary>
    /// <param name="secretName">The secret the file stages, used only to make the name recognizable.</param>
    /// <returns>A path under the system temp directory that does not yet exist.</returns>
    public static string NewStagingPath(string secretName) =>
        Path.Combine(Path.GetTempPath(), $"{secretName}-{Guid.NewGuid():N}.tmp");

    /// <summary>
    /// Creates the staging file exclusively with owner read-write permissions where the platform supports them,
    /// then writes the credential text without a trailing newline.
    /// </summary>
    /// <param name="stagingPath">The path to create; must not exist.</param>
    /// <param name="value">The credential text.</param>
    public static void WriteStagingFile(string stagingPath, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using var stream = new FileStream(stagingPath, options);
            stream.Write(bytes);
            stream.Flush();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Adds the credential to a serialized compose document: a top-level external secret and a service-level
    /// mount of it at <c>/run/secrets/{MountTarget}</c> with mode <see cref="MountMode"/>.
    /// </summary>
    /// <param name="composeDocument">The serialized compose document root.</param>
    /// <param name="serviceName">The compose service that mounts the credential.</param>
    /// <param name="secretName">The podman secret name.</param>
    public static void AttachCredentialSecret(JsonObject composeDocument, string serviceName, string secretName)
    {
        var service = composeDocument["services"]?[serviceName] as JsonObject
            ?? throw new InvalidOperationException($"Compose document has no '{serviceName}' service.");
        service["secrets"] = new JsonArray(new JsonObject
        {
            ["source"] = secretName,
            ["target"] = MountTarget,
            ["mode"] = MountMode,
        });
        composeDocument["secrets"] = new JsonObject
        {
            [secretName] = new JsonObject { ["external"] = true },
        };
    }
}
