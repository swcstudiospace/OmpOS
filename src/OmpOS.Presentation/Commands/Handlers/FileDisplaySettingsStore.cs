using System.Text.Json;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Keeps the display switches in one small JSON file that OmpOS owns, for example <c>display.json</c> in the user's OmpOS configuration folder. Every read reads the file again. A file that is missing,
/// damaged or written by another version reads as the defaults and is never an error; a write goes to a temporary file in the same folder that then replaces the file, so a crash leaves the old file or the new one.
/// </summary>
public sealed class FileDisplaySettingsStore : IDisplaySettingsStore
{
    private const int CurrentVersion = 1;

    private readonly string _path;

    /// <summary>Creates a store.</summary>
    /// <param name="path">The file. Its folder is created on the first save.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public FileDisplaySettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <inheritdoc/>
    public async Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return DisplaySettingsValues.Defaults;
            }

            var bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize(bytes, DisplaySettingsJsonContext.Default.DisplaySettingsDocument);
            return document is { Version: CurrentVersion }
                ? new DisplaySettingsValues(document.VimMode, document.ShowLineNumbers, document.ShowThinkingBlocks, document.ShowStatusIndicators, document.ShowStatusLineRunningTime, document.RewindEnabled)
                : DisplaySettingsValues.Defaults;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return DisplaySettingsValues.Defaults;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new DisplaySettingsDocument
        {
            Version = CurrentVersion,
            VimMode = values.VimMode,
            ShowLineNumbers = values.ShowLineNumbers,
            ShowThinkingBlocks = values.ShowThinkingBlocks,
            ShowStatusIndicators = values.ShowStatusIndicators,
            ShowStatusLineRunningTime = values.ShowStatusLineRunningTime,
            RewindEnabled = values.RewindEnabled,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, DisplaySettingsJsonContext.Default.DisplaySettingsDocument);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
