using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Reads and writes <c>state.json</c>. A write goes to a temporary file that is flushed and then renamed over the target, so
/// a reader sees either the old record or the new one.
/// </summary>
public sealed class OmpManagedStateStore
{
    private readonly string _path;
    private readonly string _directory;

    /// <summary>Creates a store for one state file.</summary>
    /// <param name="statePath">The state file path. Its directory is created on the first write.</param>
    /// <exception cref="ArgumentException">The path is empty or names no file.</exception>
    public OmpManagedStateStore(string statePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        _path = Path.GetFullPath(statePath);
        _directory = Path.GetDirectoryName(_path) ?? throw new ArgumentException("The state path must name a file.", nameof(statePath));
    }

    /// <summary>The full path of the state file.</summary>
    public string StatePath => _path;

    /// <summary>Reads the state. A missing file is <see cref="OmpManagedState.Empty"/>.</summary>
    /// <returns>The state record.</returns>
    /// <exception cref="InvalidDataException">The file is not a state record.</exception>
    public OmpManagedState Load()
    {
        if (!File.Exists(_path))
        {
            return OmpManagedState.Empty;
        }

        var bytes = File.ReadAllBytes(_path);
        try
        {
            return JsonSerializer.Deserialize(bytes, OmpManagedStateJsonContext.Default.OmpManagedState) ?? OmpManagedState.Empty;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The managed omp state file is not valid.", exception);
        }
    }

    /// <summary>Writes the state through a temporary file and an atomic rename.</summary>
    /// <param name="state">The record to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    /// <exception cref="IOException">The write or the rename was refused. The previous state file is unchanged.</exception>
    public void Save(OmpManagedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, OmpManagedStateJsonContext.Default.OmpManagedState);
        Directory.CreateDirectory(_directory);
        var temporary = _path + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        var moved = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, _path, overwrite: true);
            moved = true;
        }
        finally
        {
            if (!moved)
            {
                OmpCleanup.DeleteFile(temporary);
            }
        }
    }
}
