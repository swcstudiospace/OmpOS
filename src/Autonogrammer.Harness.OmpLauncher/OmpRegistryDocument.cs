using System.Text.Json;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Reads the npm packument for the omp package into an <see cref="OmpRelease"/>.</summary>
internal static class OmpRegistryDocument
{
    private const string Sha512Prefix = "sha512-";

    /// <summary>Reads the release named by <paramref name="requestedVersion"/>, or the <c>latest</c> dist-tag when it is null.</summary>
    /// <param name="document">The packument bytes.</param>
    /// <param name="requestedVersion">The version to read, or null for <c>latest</c>.</param>
    /// <returns>The release.</returns>
    /// <exception cref="InvalidDataException">The document is not a usable package record.</exception>
    public static OmpRelease ParseRelease(byte[] document, string? requestedVersion)
    {
        try
        {
            using var parsed = JsonDocument.Parse(new ReadOnlyMemory<byte>(document));
            return Read(parsed.RootElement, requestedVersion);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("the document is not JSON", exception);
        }
    }

    private static OmpRelease Read(JsonElement root, string? requestedVersion)
    {
        var version = requestedVersion ?? Text(Member(root, "dist-tags"), "latest");
        if (!OmpSemver.TryParse(version, out _))
        {
            throw new InvalidDataException("the version " + version + " is not a plain semantic version");
        }

        var manifest = Member(Member(root, "versions"), version);
        var dist = Member(manifest, "dist");
        var tarball = Text(dist, "tarball");
        if (!Uri.TryCreate(tarball, UriKind.Absolute, out var tarballUri) || !string.Equals(tarballUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new InvalidDataException("the tarball address is not https");
        }

        return new OmpRelease(version, tarballUri, Sha512Hex(Text(dist, "integrity")), EnginesBun(manifest));
    }

    private static JsonElement Member(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value))
        {
            return value;
        }

        throw new InvalidDataException("the document has no " + name + " member");
    }

    private static string Text(JsonElement parent, string name)
    {
        var value = Member(parent, name);
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text)
        {
            return text;
        }

        throw new InvalidDataException("the " + name + " member is not a string");
    }

    private static string? EnginesBun(JsonElement manifest)
    {
        if (manifest.ValueKind == JsonValueKind.Object
            && manifest.TryGetProperty("engines", out var engines)
            && engines.ValueKind == JsonValueKind.Object
            && engines.TryGetProperty("bun", out var bun)
            && bun.ValueKind == JsonValueKind.String)
        {
            return bun.GetString();
        }

        return null;
    }

    private static string Sha512Hex(string integrity)
    {
        foreach (var token in integrity.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!token.StartsWith(Sha512Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var digest = DecodeBase64(token[Sha512Prefix.Length..]);
            if (digest.Length != 64)
            {
                throw new InvalidDataException("the sha512 integrity value is not 64 bytes");
            }

            return Convert.ToHexString(digest);
        }

        throw new InvalidDataException("the registry did not publish a sha512 integrity value");
    }

    private static byte[] DecodeBase64(string text)
    {
        try
        {
            return Convert.FromBase64String(text);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("the integrity value is not base64", exception);
        }
    }
}
