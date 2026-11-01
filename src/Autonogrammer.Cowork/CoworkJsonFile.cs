using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Autonogrammer.Cowork;

/// <summary>Atomic owner-only JSON file used by the Cowork account stores. Unknown members stay on the document.</summary>
/// <typeparam name="T">The document type.</typeparam>
internal sealed class CoworkJsonFile<T>
    where T : class
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly Lock _gate = new();
    private readonly int _maxBytes;
    private readonly Func<T> _empty;
    private readonly JsonTypeInfo<T> _typeInfo;

    /// <summary>Initializes a new instance of the <see cref="CoworkJsonFile{T}"/> class.</summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="maxBytes">The size cap.</param>
    /// <param name="empty">Creates an empty document.</param>
    /// <param name="typeInfo">The source-generated type info.</param>
    public CoworkJsonFile(string filePath, int maxBytes, Func<T> empty, JsonTypeInfo<T> typeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        _maxBytes = maxBytes;
        _empty = empty;
        _typeInfo = typeInfo;
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>Reads the document. A missing file is empty.</summary>
    /// <returns>The document.</returns>
    public T Read()
    {
        lock (_gate)
        {
            return ReadUnlocked();
        }
    }

    /// <summary>Applies a change and writes the document atomically.</summary>
    /// <param name="change">The change.</param>
    public void Update(Action<T> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            var document = ReadUnlocked();
            change(document);
            Write(document);
        }
    }

    private T ReadUnlocked()
    {
        if (!File.Exists(FilePath))
        {
            return _empty();
        }

        var bytes = ReadBytes();
        if (bytes.Length > _maxBytes)
        {
            throw new InvalidDataException("The Cowork file is over the size cap: " + Path.GetFileName(FilePath));
        }

        try
        {
            return JsonSerializer.Deserialize(bytes, _typeInfo)
                ?? throw new InvalidDataException("The Cowork file is not valid: " + Path.GetFileName(FilePath));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Cowork file is not valid: " + Path.GetFileName(FilePath), exception);
        }
    }

    private byte[] ReadBytes()
    {
        try
        {
            return File.ReadAllBytes(FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The Cowork file could not be read: " + Path.GetFileName(FilePath), exception);
        }
    }

    private void Write(T document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, _typeInfo);
        if (bytes.Length > _maxBytes)
        {
            throw new InvalidDataException("The Cowork file is over the size cap: " + Path.GetFileName(FilePath));
        }

        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            WriteTemporary(temporary, bytes);
            File.Move(temporary, FilePath, overwrite: true);
            replaced = true;
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(FilePath, OwnerFile);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The Cowork file could not be written: " + Path.GetFileName(FilePath), exception);
        }
        finally
        {
            if (!replaced)
            {
                TryDelete(temporary);
            }
        }
    }

    private static void WriteTemporary(string temporary, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(temporary) ?? ".";
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, OwnerDirectory);
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFile;
        }
        using var stream = new FileStream(temporary, options);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
