using System.Text.Json;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// The head's preferences: <c>settings.json</c> in the <c>config</c> folder of the state directory. It holds the motion setting, the
/// terminal auto-run choice and the runtime windows, and never key material: the engine owns models, providers and credentials.
/// </summary>
/// <remarks>
/// <para>
/// A write keeps every member this build does not know, so a newer build of the same major schema loses nothing. It goes to a
/// temporary file created with mode 0600 in the same folder, is flushed to disk and renamed over the file; the folder is created with
/// mode 0700. A file that is unreadable, not valid, from a newer major schema or holding an unusable value is an error state: every
/// read and every write then fails with a <see cref="SettingsSourceException"/> that says why, and the file is never overwritten.
/// </para>
/// </remarks>
internal sealed class FileSettingsStore
{
    /// <summary>The major version of the schema this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The largest settings file that is read, in bytes.</summary>
    public const int MaxFileBytes = 64 * 1024;

    /// <summary>The folder of the state directory that holds the file.</summary>
    public const string DirectoryName = "config";

    /// <summary>The name of the settings file.</summary>
    public const string FileName = "settings.json";

    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const string MotionMember = "motion";
    private const string IdleStopMember = "runtime.idleStopHours";
    private const string RetentionMember = "runtime.retentionDays";

    private readonly System.Threading.Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="FileSettingsStore"/> class.</summary>
    /// <param name="stateRoot">The state directory. The file is <c>config/settings.json</c> below it.</param>
    /// <exception cref="ArgumentException"><paramref name="stateRoot"/> is null, empty or white space.</exception>
    public FileSettingsStore(string stateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ConfigDirectory = Path.Combine(Path.GetFullPath(stateRoot), DirectoryName);
        FilePath = Path.Combine(ConfigDirectory, FileName);
    }

    /// <summary>Occurs after a change was written. A reader reads the values again.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the folder that holds the file.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Gets the full path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>Reads the values. A file that does not exist gives the defaults.</summary>
    /// <returns>The values.</returns>
    /// <exception cref="SettingsSourceException">The file is unreadable, not valid, from a newer major schema or holds an unusable value.</exception>
    public StoredSettings Read()
    {
        lock (_gate)
        {
            return ToValues(ReadDocument());
        }
    }

    /// <summary>Saves the motion setting.</summary>
    /// <param name="motion">The setting.</param>
    /// <exception cref="SettingsSourceException">The file cannot be read or written; nothing is changed.</exception>
    public void SetMotion(MotionPreference motion) =>
        Update(document => document.Motion = motion switch
        {
            MotionPreference.Reduced => "reduced",
            MotionPreference.Full => "full",
            _ => "system",
        });

    /// <summary>Saves the terminal auto-run choice.</summary>
    /// <param name="autoRun">True to run proposed terminal commands without an approval.</param>
    /// <exception cref="SettingsSourceException">The file cannot be read or written; nothing is changed.</exception>
    public void SetTerminalAutoRun(bool autoRun) =>
        Update(document =>
        {
            document.Terminal ??= new SettingsTerminalSection();
            document.Terminal.AutoRun = autoRun;
        });

    /// <summary>Saves the runtime windows.</summary>
    /// <param name="values">The values, within <see cref="RuntimeSettingsLimits"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="values"/> is outside the limits.</exception>
    /// <exception cref="SettingsSourceException">The file cannot be read or written; nothing is changed.</exception>
    public void SetRuntime(RuntimeSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!RuntimeSettingsLimits.AreInRange(values))
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The runtime settings are outside their limits.");
        }

        Update(document =>
        {
            document.Runtime ??= new SettingsRuntimeSection();
            document.Runtime.IdleStopHours = values.IdleStopHours;
            document.Runtime.RetentionDays = values.RetentionDays;
        });
    }

    private static StoredSettings ToValues(SettingsDocument document, string path)
    {
        var motion = document.Motion switch
        {
            null => MotionPreference.System,
            { } text when string.Equals(text, "system", StringComparison.OrdinalIgnoreCase) => MotionPreference.System,
            { } text when string.Equals(text, "reduced", StringComparison.OrdinalIgnoreCase) => MotionPreference.Reduced,
            { } text when string.Equals(text, "full", StringComparison.OrdinalIgnoreCase) => MotionPreference.Full,
            _ => throw new SettingsSourceException(HeadSettingsCopy.SettingsBadMember(path, MotionMember)),
        };

        var idle = document.Runtime?.IdleStopHours ?? RuntimeSettingsLimits.DefaultIdleStopHours;
        if (idle is < RuntimeSettingsLimits.MinIdleStopHours or > RuntimeSettingsLimits.MaxIdleStopHours)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsBadMember(path, IdleStopMember));
        }

        var retention = document.Runtime?.RetentionDays ?? RuntimeSettingsLimits.DefaultRetentionDays;
        if (retention is < RuntimeSettingsLimits.MinRetentionDays or > RuntimeSettingsLimits.MaxRetentionDays)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsBadMember(path, RetentionMember));
        }

        return new StoredSettings(motion, document.Terminal?.AutoRun ?? false, new RuntimeSettingsValues(idle, retention));
    }

    private StoredSettings ToValues(SettingsDocument document) => ToValues(document, FilePath);

    private SettingsDocument ReadDocument()
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(FilePath))
            {
                return new SettingsDocument { SchemaVersion = CurrentSchemaVersion };
            }

            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > MaxFileBytes)
            {
                throw new SettingsSourceException(HeadSettingsCopy.SettingsTooLarge(FilePath));
            }

            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsUnreadable(FilePath), exception);
        }

        SettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, SettingsJsonContext.Default.SettingsDocument);
        }
        catch (JsonException exception)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsInvalid(FilePath), exception);
        }

        if (document is null)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsInvalid(FilePath));
        }

        var version = document.SchemaVersion ?? CurrentSchemaVersion;
        if (version > CurrentSchemaVersion)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsNewer(FilePath, version));
        }

        if (version < CurrentSchemaVersion)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsInvalid(FilePath));
        }

        return document;
    }

    private void Update(Action<SettingsDocument> change)
    {
        lock (_gate)
        {
            var document = ReadDocument();

            // A file with an unusable value is an error state: it is reported, never repaired by overwriting it.
            _ = ToValues(document);
            change(document);
            document.SchemaVersion = CurrentSchemaVersion;
            Write(document);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Write(SettingsDocument document)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            EnsureDirectory();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SettingsJsonContext.Default.SettingsDocument);
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

            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, FilePath, overwrite: true);
            replaced = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new SettingsSourceException(HeadSettingsCopy.SettingsNotWritten(FilePath), exception);
        }
        finally
        {
            if (!replaced)
            {
                TryDelete(temporary);
            }
        }
    }

    private void EnsureDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(ConfigDirectory);
            return;
        }

        var existed = Directory.Exists(ConfigDirectory);
        Directory.CreateDirectory(ConfigDirectory, OwnerDirectory);
        if (existed && new DirectoryInfo(ConfigDirectory).LinkTarget is null && File.GetUnixFileMode(ConfigDirectory) != OwnerDirectory)
        {
            File.SetUnixFileMode(ConfigDirectory, OwnerDirectory);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless: it holds a copy of the settings and has mode 0600.
        }
    }
}
