using System.Text.Json;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Owner-only atomic store of which bundle skills are enabled. A missing key means enabled.</summary>
public sealed class SkillStateStore
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="SkillStateStore"/> class.</summary>
    /// <param name="path">The full path of <c>skills-state.json</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public SkillStateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>Reads whether a skill is enabled. A skill that has never been toggled is enabled.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="skillId">The skill folder name.</param>
    /// <returns><see langword="true"/> when the skill should load.</returns>
    public bool IsEnabled(string bundleId, string skillId)
    {
        var key = Key(bundleId, skillId);
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return true;
            }

            var document = JsonSerializer.Deserialize(File.ReadAllText(_path), SkillStateJsonContext.Default.SkillStateDocument);
            if (document is null || !document.Skills.TryGetValue(key, out var entry))
            {
                return true;
            }

            return entry.Enabled;
        }
    }

    /// <summary>Stores whether a skill is enabled.</summary>
    /// <param name="bundleId">The bundle id.</param>
    /// <param name="skillId">The skill folder name.</param>
    /// <param name="enabled">The new flag.</param>
    public void SetEnabled(string bundleId, string skillId, bool enabled)
    {
        var key = Key(bundleId, skillId);
        lock (_gate)
        {
            var document = File.Exists(_path)
                ? JsonSerializer.Deserialize(File.ReadAllText(_path), SkillStateJsonContext.Default.SkillStateDocument) ?? new SkillStateDocument()
                : new SkillStateDocument();
            document.Skills[key] = new SkillStateEntry { Enabled = enabled };
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    File.SetUnixFileMode(directory, OwnerDirectory);
                }
            }

            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, SkillStateJsonContext.Default.SkillStateDocument));
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(temporary, OwnerFile);
            }

            File.Move(temporary, _path, overwrite: true);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(_path, OwnerFile);
            }
        }
    }

    private static string Key(string bundleId, string skillId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        return bundleId + "/" + skillId;
    }
}
