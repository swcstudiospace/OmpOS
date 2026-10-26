using System.Text.Json;

namespace OmpOS.Harness.Providers;

/// <summary>
/// The file that holds the custom providers a user defined. Each write goes to a temporary file in the same directory, is
/// flushed to disk, and is then moved over the store file, so a failed write leaves the previous document in force. A file
/// that cannot be read or parsed makes <see cref="ReadAsync"/> throw; the store is never reset silently. The file holds no
/// key material.
/// </summary>
public sealed class ProviderRegistryStore
{
    private readonly string _path;

    /// <summary>Creates a store over a file. Nothing is read or written until a method is called.</summary>
    /// <param name="path">The store file. Its directory is created on the first write.</param>
    public ProviderRegistryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Gets the full path of the store file.</summary>
    public string FilePath => _path;

    /// <summary>Reads every stored custom provider, or none when no file exists yet.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="JsonException">The file exists but is not a valid registry of this version, or names an invalid value.</exception>
    /// <returns>The stored definitions, in file order.</returns>
    public async Task<IReadOnlyList<CustomProviderDefinition>> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }

        var document = JsonSerializer.Deserialize(bytes, ProviderRegistryJsonContext.Default.ProviderRegistryDocument)
            ?? throw new JsonException("The provider registry file is empty.");
        if (document.Version != ProviderRegistryDocument.CurrentVersion)
        {
            throw new JsonException("The provider registry file has an unsupported version.");
        }

        var providers = document.Providers ?? throw new JsonException("The provider registry file has no provider list.");
        var result = new List<CustomProviderDefinition>(providers.Count);
        foreach (var provider in providers)
        {
            result.Add(FromDocument(provider ?? throw new JsonException("The provider registry file has a null provider entry.")));
        }

        return result;
    }

    /// <summary>Replaces the stored providers with <paramref name="definitions"/> and persists them before returning.</summary>
    /// <param name="definitions">The complete list to store. Every definition must have a base endpoint.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">The file could not be written or replaced. The previous contents stay in force.</exception>
    public async Task WriteAsync(IReadOnlyList<CustomProviderDefinition> definitions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var document = new ProviderRegistryDocument
        {
            Version = ProviderRegistryDocument.CurrentVersion,
            Providers = [.. definitions.Select(ToDocument)],
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ProviderRegistryJsonContext.Default.ProviderRegistryDocument);

        EnsureDirectory();

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            await WriteTemporaryAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _path, overwrite: true);
            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                DeleteTemporary(temporary);
            }
        }
    }

    private static async Task WriteTemporaryAsync(string temporary, byte[] bytes, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            // The file names the user's endpoints, so only its owner may read or write it.
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var stream = new FileStream(temporary, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // .NET has no async flush to disk, so the fsync runs on the thread pool; it must not be cancelled mid-flush.
            await Task.Run(() => stream.Flush(flushToDisk: true), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
        {
            return;
        }

        // Only the leaf directory created by this call is restricted to its owner. Missing ancestors keep the default
        // mode, and an existing directory keeps its mode.
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (new DirectoryInfo(directory).LinkTarget is not null)
        {
            throw new IOException("The provider registry directory is a symbolic link, so its mode is not changed.");
        }

        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static CustomProviderDefinition FromDocument(CustomProviderDocument entry)
    {
        if (entry.Id is null || entry.DisplayName is null)
        {
            throw new JsonException("The provider registry file has an entry without an id or a display name.");
        }

        var transport = ParseEnum<ProviderTransport>(entry.Transport, "transport", entry.Id);
        var authType = ParseEnum<ProviderAuthType>(entry.AuthType, "auth type", entry.Id);
        if (!Uri.TryCreate(entry.BaseEndpoint, UriKind.Absolute, out var endpoint))
        {
            throw new JsonException($"The provider registry entry '{entry.Id}' has no absolute base endpoint.");
        }

        var capabilities = ProviderCapabilities.None;
        foreach (var name in entry.Capabilities ?? throw new JsonException($"The provider registry entry '{entry.Id}' has no capability list."))
        {
            var flag = ParseEnum<ProviderCapabilities>(name, "capability", entry.Id);
            if (flag == ProviderCapabilities.None || !Enum.IsDefined(flag))
            {
                throw new JsonException($"The provider registry entry '{entry.Id}' has an unknown capability '{name}'.");
            }

            capabilities |= flag;
        }

        var models = entry.Models ?? throw new JsonException($"The provider registry entry '{entry.Id}' has no model list.");
        return new CustomProviderDefinition(
            entry.Id,
            entry.DisplayName,
            transport,
            authType,
            entry.AuthHeaderName,
            endpoint,
            [.. models],
            capabilities,
            entry.AllowRemotePlainHttp);
    }

    private static TEnum ParseEnum<TEnum>(string? text, string what, string id)
        where TEnum : struct, Enum
    {
        // Only the canonical member name is accepted: "1", "Anthropic, Other" and lowercase forms are refused, so the file
        // cannot name a value in a way that later changes meaning.
        if (text is null
            || !Enum.TryParse<TEnum>(text, ignoreCase: false, out var value)
            || !Enum.IsDefined(value)
            || !string.Equals(value.ToString(), text, StringComparison.Ordinal))
        {
            throw new JsonException($"The provider registry entry '{id}' has an unknown {what}.");
        }

        return value;
    }

    private static CustomProviderDocument ToDocument(CustomProviderDefinition definition)
    {
        var endpoint = definition.BaseEndpoint
            ?? throw new ArgumentException($"The provider '{definition.Id}' has no base endpoint to store.", nameof(definition));
        var flags = new List<string>();
        foreach (var flag in Enum.GetValues<ProviderCapabilities>())
        {
            if (flag != ProviderCapabilities.None && definition.Capabilities.HasFlag(flag))
            {
                flags.Add(flag.ToString());
            }
        }

        return new CustomProviderDocument
        {
            Id = definition.Id,
            DisplayName = definition.DisplayName,
            Transport = definition.Transport.ToString(),
            AuthType = definition.AuthType.ToString(),
            AuthHeaderName = definition.AuthHeaderName,
            BaseEndpoint = endpoint.OriginalString,
            Models = [.. definition.Models],
            Capabilities = flags,
            AllowRemotePlainHttp = definition.AllowRemotePlainHttp,
        };
    }

    // Runs only while a write is failing, so the exception already in flight is the one reported. A temporary file that
    // cannot be deleted is left behind; the store reads only its own path, so the leftover file is never used.
    private static void DeleteTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
