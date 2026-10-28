using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace OmpOS.Harness.Mcp.Config;

/// <summary>
/// Computes the consent identity of a server as declared. The identity covers the command, arguments, environment, URL and headers
/// exactly as written, before variable expansion, so the same repository gives the same identity on every machine and under every checkout
/// directory, while a change to anything the server is started or reached with gives a new identity and consent for the old one no longer applies.
/// It does not cover the content of a script the command names: approve a server for what the file declares, and review scripts as code.
/// The name, the file it came from and the enabled flag are not hashed.
/// </summary>
public static class McpConsentIdentity
{
    /// <summary>The version this class writes. It is part of the hashed document, so a future change cannot collide with this one.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Returns the lowercase SHA-256 hex digest of the declaration's canonical JSON form.</summary>
    /// <param name="transport">How the server is reached.</param>
    /// <param name="command">The command as written, or <see langword="null"/>.</param>
    /// <param name="args">The arguments as written.</param>
    /// <param name="env">The environment as written.</param>
    /// <param name="address">The URL as written, or <see langword="null"/>.</param>
    /// <param name="headers">The headers as written.</param>
    /// <returns>A 64-character lowercase hex string.</returns>
    public static string Compute(
        McpTransportKind transport,
        string? command,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string> env,
        string? address,
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(headers);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", CurrentVersion);
            writer.WriteString("t", transport.ToString());
            writer.WriteString("c", command);
            writer.WriteStartArray("a");
            foreach (var argument in args)
            {
                writer.WriteStringValue(argument);
            }

            writer.WriteEndArray();
            WritePairs(writer, "e", env);
            writer.WriteString("u", address);
            WritePairs(writer, "h", headers);
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WritePairs(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> pairs)
    {
        writer.WriteStartArray(name);
        foreach (var pair in pairs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(pair.Key);
            writer.WriteStringValue(pair.Value);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}
