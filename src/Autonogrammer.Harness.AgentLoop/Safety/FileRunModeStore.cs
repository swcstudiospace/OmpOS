using Autonogrammer.Harness.Abstractions;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Keeps the run mode in <c>~/.autonogrammer/run-mode.json</c> as <c>{ "mode": "auto-review" }</c>. A head may use another <see cref="IRunModeStore"/> instead.</summary>
public sealed class FileRunModeStore : IRunModeStore
{
    private readonly string _home;
    private readonly string _path;

    /// <summary>Gets the notices from the most recent load. Empty when the current file exists or neither file exists.</summary>
    public IReadOnlyList<LegacySafetyNotice> Notices { get; private set; } = [];

    /// <summary>Initializes a new instance of the <see cref="FileRunModeStore"/> class.</summary>
    /// <param name="homeDirectory">The home directory.</param>
    /// <exception cref="ArgumentException"><paramref name="homeDirectory"/> is blank.</exception>
    public FileRunModeStore(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        _home = homeDirectory;
        _path = Path.Combine(homeDirectory, SafetyFileLocations.AutonogrammerFolder, RunModeStoreFile.FileName);
    }

    /// <inheritdoc/>
    public async Task<RunMode> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Notices = [];
            var path = _path;
            if (!File.Exists(_path))
            {
                var legacy = Path.Combine(_home, LegacyConventionDiscovery.LegacyDirectoryName, RunModeStoreFile.FileName);
                if (File.Exists(legacy))
                {
                    path = legacy;
                    Notices =
                    [
                        new LegacySafetyNotice("user", legacy, true, "Conflict: run-mode.json in the deprecated directory was read. Save writes the current path only."),
                    ];
                }
            }

            if (!File.Exists(path))
            {
                return RunModes.Default;
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("mode", out var element)
                && element.ValueKind == JsonValueKind.String
                && RunModes.TryParse(element.GetString(), out var mode)
                    ? mode
                    : RunModes.Default;
        }
        catch (JsonException)
        {
            return RunModes.Default;
        }
        catch (IOException)
        {
            return RunModes.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return RunModes.Default;
        }
    }

    /// <inheritdoc/>
    public Task<SafetyEditResult> SaveAsync(RunMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = RunModes.WireName(mode);
        return Task.FromResult(AutonogrammerJsonFile.Update(_path, root => root["mode"] = JsonValue.Create(name)));
    }
}
