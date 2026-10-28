using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Reads and validates an environment file: <c>.autonogrammer/environment.json</c> or Cursor's <c>.cursor/environment.json</c> (members <c>snapshot</c>,
/// <c>install</c>, <c>start</c>, <c>terminals</c> and <c>build.{dockerfile,context}</c>; <c>$schema</c> is accepted and ignored). This type never
/// executes anything and never touches the network: it turns text into a validated <see cref="EnvironmentConfig"/> or a list of errors. Paths in
/// <c>build</c> are relative to the folder that holds the file, as in Cursor, except that a bare <c>.</c> and <c>..</c> from <c>.cursor</c> both mean the
/// repository root; a path that leaves the repository is an error.
/// </summary>
internal static class EnvironmentConfigFile
{
    /// <summary>The path of the Autonogrammer file in a repository.</summary>
    public const string AutonogrammerPath = ".autonogrammer/environment.json";

    /// <summary>The path of Cursor's file in a repository.</summary>
    public const string CursorPath = ".cursor/environment.json";

    /// <summary>The largest file accepted, in characters.</summary>
    public const int MaximumFileLength = 65_536;

    private const int MaximumCommandLength = 4_096;
    private const int MaximumTerminals = 16;
    private const int MaximumNameLength = 64;

    private static readonly HashSet<string> KnownMembers = new(StringComparer.Ordinal) { "$schema", "snapshot", "install", "start", "terminals", "build" };

    private static readonly JsonDocumentOptions ParseOptions = new() { MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow };

    /// <summary>Parses and validates the text of an environment file.</summary>
    /// <param name="json">The file text.</param>
    /// <param name="source">The path or label recorded as <see cref="EnvironmentConfig.Source"/>; also decides which folder <c>build</c> paths are relative to.</param>
    /// <returns>The configuration, or the errors that refused it.</returns>
    public static EnvironmentConfigParseResult Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(source);
        var errors = new List<string>();
        var warnings = new List<string>();
        if (json.Length > MaximumFileLength)
        {
            return new EnvironmentConfigParseResult(null, ["The file is longer than 65536 characters."], warnings);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, ParseOptions);
        }
        catch (JsonException)
        {
            return new EnvironmentConfigParseResult(null, ["The file is not valid JSON."], warnings);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new EnvironmentConfigParseResult(null, ["The file must hold a JSON object."], warnings);
            }

            return ReadMembers(root, source, errors, warnings);
        }
    }

    private static EnvironmentConfigParseResult ReadMembers(JsonElement root, string source, List<string> errors, List<string> warnings)
    {
        string? snapshot = null;
        string? install = null;
        string? start = null;
        var terminals = new List<EnvironmentTerminal>();
        EnvironmentBuild? build = null;
        foreach (var member in root.EnumerateObject())
        {
            switch (member.Name)
            {
                case "snapshot":
                    snapshot = ReadString(member, errors);
                    break;
                case "install":
                    install = ReadString(member, errors);
                    break;
                case "start":
                    start = ReadString(member, errors);
                    break;
                case "terminals":
                    ReadTerminals(member.Value, terminals, errors);
                    break;
                case "build":
                    build = ReadBuild(member.Value, source, errors);
                    break;
                default:
                    if (!KnownMembers.Contains(member.Name))
                    {
                        warnings.Add("An unknown member was ignored.");
                    }

                    break;
            }
        }

        return errors.Count > 0
            ? new EnvironmentConfigParseResult(null, errors, warnings)
            : new EnvironmentConfigParseResult(new EnvironmentConfig(snapshot, install, start, terminals, build, source), [], warnings);
    }

    /// <summary>Reads <c>.autonogrammer/environment.json</c> and, when absent, <c>.cursor/environment.json</c> from a checked-out directory.</summary>
    /// <param name="repositoryRoot">The repository root directory.</param>
    /// <returns>The result for the first file found, or null when neither exists.</returns>
    public static EnvironmentConfigParseResult? ReadFromDirectory(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        foreach (var relative in new[] { AutonogrammerPath, CursorPath })
        {
            var path = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return info.Length > MaximumFileLength
                    ? new EnvironmentConfigParseResult(null, ["The file is longer than 65536 characters."], [])
                    : Parse(File.ReadAllText(path), relative);
            }
        }

        return null;
    }

    private static string? ReadString(JsonProperty member, List<string> errors)
    {
        if (member.Value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (member.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add($"'{member.Name}' must be a string.");
            return null;
        }

        var text = member.Value.GetString()!;
        if (text.Length > MaximumCommandLength)
        {
            errors.Add("'" + member.Name + "' is longer than 4096 characters.");
            return null;
        }

        if (text.Contains('\0', StringComparison.Ordinal))
        {
            errors.Add($"'{member.Name}' contains a NUL character.");
            return null;
        }

        return text.Length == 0 ? null : text;
    }

    private static void ReadTerminals(JsonElement value, List<EnvironmentTerminal> terminals, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add("'terminals' must be an array.");
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (terminals.Count >= MaximumTerminals)
            {
                errors.Add("'terminals' holds more than 16 entries.");
                return;
            }

            var label = string.Create(CultureInfo.InvariantCulture, $"terminals[{index}]");
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add(label + " must be an object.");
                continue;
            }

            var name = item.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String ? nameValue.GetString() : null;
            var command = item.TryGetProperty("command", out var commandValue) && commandValue.ValueKind == JsonValueKind.String ? commandValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength)
            {
                errors.Add(label + ".name is required and at most 64 characters.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(command) || command.Length > MaximumCommandLength || command.Contains('\0', StringComparison.Ordinal))
            {
                errors.Add(label + ".command is required and at most 4096 characters.");
                continue;
            }

            terminals.Add(new EnvironmentTerminal(name, command));
        }
    }

    private static EnvironmentBuild? ReadBuild(JsonElement value, string source, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            errors.Add("'build' must be an object.");
            return null;
        }

        var baseFolder = ConfigFolder(source);
        var dockerfile = ReadBuildPath(value, "dockerfile", baseFolder, errors);
        var context = ReadBuildPath(value, "context", baseFolder, errors);
        return new EnvironmentBuild(dockerfile, context);
    }

    private static string? ReadBuildPath(JsonElement build, string member, string baseFolder, List<string> errors)
    {
        if (!build.TryGetProperty(member, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            errors.Add($"'build.{member}' must be a non-empty string.");
            return null;
        }

        var text = value.GetString()!;
        if (text.Contains('\0', StringComparison.Ordinal) || text.Contains('\\', StringComparison.Ordinal) || text.StartsWith('/'))
        {
            errors.Add($"'build.{member}' must be a relative path with forward slashes.");
            return null;
        }

        // Cursor documents a bare dot, like a double dot, as the repository root (UNVERIFIED against its published schema file).
        var segments = baseFolder.Length == 0 || string.Equals(text, ".", StringComparison.Ordinal) ? new List<string>() : new List<string>(baseFolder.Split('/'));
        foreach (var segment in text.Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (segments.Count == 0)
                {
                    errors.Add($"'build.{member}' leaves the repository.");
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }

    private static string ConfigFolder(string source)
    {
        var slash = source.LastIndexOf('/');
        return slash <= 0 ? string.Empty : source[..slash];
    }
}
