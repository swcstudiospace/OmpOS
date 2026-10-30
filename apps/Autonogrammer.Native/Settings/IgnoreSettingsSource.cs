using System.Globalization;
using Autonogrammer.Presentation.Settings.Ignore;
using Autonogrammer.Workspace.Ignore;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The head's source of the Ignore page. The global patterns and the two switches live in <c>settings.json</c> (<see cref="FileSettingsStore"/>);
/// the ignore files are read from the workspace root with <see cref="PhysicalIgnoreFileSource"/>, which never follows a symbolic link.
/// </summary>
internal sealed class IgnoreSettingsSource : IIgnoreSettingsSource
{
    private const string GitIgnoreName = ".gitignore";

    private readonly FileSettingsStore _store;
    private readonly string _workspaceRoot;

    /// <summary>Initializes a new instance of the <see cref="IgnoreSettingsSource"/> class.</summary>
    /// <param name="store">The settings file that holds the values.</param>
    /// <param name="workspaceRoot">The workspace root the ignore files are looked for in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    public IgnoreSettingsSource(FileSettingsStore store, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _store = store;
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> DefaultPatterns => IgnoreDefaults.Patterns;

    /// <summary>Builds the matcher options a saved value stands for: the global patterns and the built-in list switch. Every other option keeps its default.</summary>
    /// <param name="values">The saved values.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public static IgnoreOptions ToIgnoreOptions(IgnoreSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new IgnoreOptions { GlobalPatterns = [.. values.GlobalPatterns], UseDefaultList = values.UseDefaultList };
    }

    /// <inheritdoc/>
    public Task<IgnoreSettingsValues> LoadAsync(CancellationToken cancellationToken) => SettingsTask.ReadAsync(_store.ReadIgnore);

    /// <inheritdoc/>
    public Task SaveAsync(IgnoreSettingsValues values, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        return SettingsTask.WriteAsync(() => _store.SetIgnore(values));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IgnoreFileRow>> FoundFilesAsync(CancellationToken cancellationToken) => SettingsTask.ReadAsync(ReadFiles);

    private IReadOnlyList<IgnoreFileRow> ReadFiles()
    {
        var options = new IgnoreOptions { UseDefaultList = false };
        var source = new PhysicalIgnoreFileSource(_workspaceRoot);
        var rows = new List<IgnoreFileRow>();
        string[] names = [.. options.IgnoreFileNames, GitIgnoreName];
        foreach (var name in names)
        {
            var text = source.ReadText(name, options.MaxFileBytes);
            if (text is null)
            {
                continue;
            }

            var matcher = IgnoreMatcher.Create(options);
            var count = matcher.AddFile(string.Empty, text, name);
            var problems = matcher.Diagnostics
                .Select(static diagnostic => diagnostic.Line > 0
                    ? string.Create(CultureInfo.InvariantCulture, $"Line {diagnostic.Line}: {diagnostic.Message}")
                    : diagnostic.Message)
                .ToArray();
            rows.Add(new IgnoreFileRow(name, count, problems));
        }

        return rows;
    }
}
