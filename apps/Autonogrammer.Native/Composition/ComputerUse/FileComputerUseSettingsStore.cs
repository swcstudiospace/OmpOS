using System.Text.Json;
using System.Text.Json.Serialization;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Native.Composition.ComputerUse;


/// <summary>
/// Owner-only <c>computer-use/settings.json</c> under the state directory. An unknown version, an oversize file or a
/// malformed file yields the defaults and a reported failure. It never throws on those reads.
/// </summary>
internal sealed class FileComputerUseSettingsStore : IComputerUseSettingsStore
{
    /// <summary>Schema this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Largest file this store reads.</summary>
    public const int MaxBytes = 64 * 1024;

    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _directory;
    private readonly Lock _gate = new();

    /// <summary>Creates the store.</summary>
    /// <param name="stateDirectory">State directory. The file is <c>computer-use/settings.json</c> under it.</param>
    /// <exception cref="ArgumentException"><paramref name="stateDirectory"/> is empty.</exception>
    public FileComputerUseSettingsStore(string stateDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        _directory = Path.Combine(stateDirectory, "computer-use");
    }

    /// <summary>Gets the settings file path.</summary>
    public string FilePath => Path.Combine(_directory, "settings.json");

    /// <summary>Gets the last reported failure, or null.</summary>
    public string? LastFailure { get; private set; }

    /// <inheritdoc />
    public ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            LastFailure = null;
            if (!File.Exists(FilePath))
            {
                return ValueTask.FromResult(new ComputerUseUserSettings());
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(FilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LastFailure = "computer-use settings could not be read";
                return ValueTask.FromResult(new ComputerUseUserSettings());
            }

            if (bytes.Length > MaxBytes)
            {
                LastFailure = "computer-use settings exceed 64 KiB";
                return ValueTask.FromResult(new ComputerUseUserSettings());
            }

            ComputerUseSettingsFile? file;
            try
            {
                file = JsonSerializer.Deserialize(bytes, ComputerUseSettingsJsonContext.Default.ComputerUseSettingsFile);
            }
            catch (JsonException)
            {
                LastFailure = "computer-use settings are malformed";
                return ValueTask.FromResult(new ComputerUseUserSettings());
            }

            if (file is null || file.SchemaVersion != CurrentSchemaVersion)
            {
                LastFailure = "computer-use settings have an unknown version";
                return ValueTask.FromResult(new ComputerUseUserSettings());
            }

            return ValueTask.FromResult(new ComputerUseUserSettings
            {
                KillChord = file.KillChord,
                SecurityPhrase = file.SecurityPhrase,
                AuditThumbnails = file.AuditThumbnails,
                DenylistAdditions = file.DenylistAdditions ?? [],
            });
        }
    }

    /// <inheritdoc />
    public ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var file = new ComputerUseSettingsFile
        {
            SchemaVersion = CurrentSchemaVersion,
            KillChord = settings.KillChord,
            SecurityPhrase = settings.SecurityPhrase,
            AuditThumbnails = settings.AuditThumbnails,
            DenylistAdditions = settings.DenylistAdditions.ToList(),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, ComputerUseSettingsJsonContext.Default.ComputerUseSettingsFile);
        if (bytes.Length > MaxBytes)
        {
            lock (_gate)
            {
                LastFailure = "computer-use settings exceed 64 KiB";
            }

            return ValueTask.CompletedTask;
        }

        lock (_gate)
        {
            Write(bytes);
        }

        return ValueTask.CompletedTask;
    }

    private void Write(byte[] bytes)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(_directory);
            }
            else
            {
                Directory.CreateDirectory(_directory, OwnerDirectory);
            }

            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFile;
            }

            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, FilePath, overwrite: true);
            replaced = true;
            LastFailure = null;
        }
        finally
        {
            if (!replaced && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
