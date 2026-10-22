using System.Security.Cryptography;
using System.Text.Json;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// Computes the identity that consent is recorded against.
/// </summary>
public static class HookCommandIdentity
{
    /// <summary>
    /// Returns the lowercase SHA-256 hex digest of the hook's canonical JSON form. The fields are hashed in the order
    /// listed in <see cref="HookIdentityDocument"/>: the argument list, the working directory, the trigger, then the
    /// blocking flag. Each argument keeps its own boundary, so two different commands never share a digest by
    /// concatenation. Changing any of these inputs gives a new digest, so consent for the old one does not apply.
    /// The enabled flag is not hashed, so enabling or disabling a hook does not need new consent.
    /// </summary>
    /// <param name="trigger">The lifecycle event that runs the hook.</param>
    /// <param name="blocking">Whether the hook may deny the action.</param>
    /// <param name="command">The command to identify.</param>
    /// <returns>A 64-character lowercase hex string.</returns>
    public static string ComputeHash(HookTrigger trigger, bool blocking, HookCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var document = new HookIdentityDocument
        {
            Arguments = [.. command.Arguments],
            WorkingDirectory = command.WorkingDirectory,
            Trigger = trigger.ToString(),
            Blocking = blocking,
        };
        var canonical = JsonSerializer.SerializeToUtf8Bytes(document, PluginJsonContext.Default.HookIdentityDocument);
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }
}
