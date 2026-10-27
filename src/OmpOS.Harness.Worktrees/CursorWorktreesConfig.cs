using System.Text.Json;

namespace OmpOS.Worktrees;

/// <summary>
/// The contents of a Cursor <c>.cursor/worktrees.json</c> file: the three setup keys Cursor documents. This type only reads and exposes
/// them. Cursor looks for the file in the worktree first and then in the project root; <see cref="Find"/> does the same. Running the
/// setup (with <c>ROOT_WORKTREE_PATH</c> set to the main checkout) is not part of this type and must go through the setup consent.
/// </summary>
public sealed class CursorWorktreesConfig
{
    /// <summary>The path of the file below a worktree or project root, with forward slashes.</summary>
    public const string RelativePath = ".cursor/worktrees.json";

    /// <summary>The environment variable Cursor sets for setup commands to the path of the main checkout.</summary>
    public const string RootWorktreePathVariable = "ROOT_WORKTREE_PATH";

    private const string GenericKey = "setup-worktree";
    private const string UnixKey = "setup-worktree-unix";
    private const string WindowsKey = "setup-worktree-windows";

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private CursorWorktreesConfig(string filePath, CursorWorktreeSetup? generic, CursorWorktreeSetup? unix, CursorWorktreeSetup? windows)
    {
        FilePath = filePath;
        Generic = generic;
        Unix = unix;
        Windows = windows;
    }

    /// <summary>Gets the absolute path of the file this was read from.</summary>
    public string FilePath { get; }

    /// <summary>Gets the <c>setup-worktree</c> entry, the fallback for every operating system, or null.</summary>
    public CursorWorktreeSetup? Generic { get; }

    /// <summary>Gets the <c>setup-worktree-unix</c> entry for macOS and Linux, or null. It takes precedence over <see cref="Generic"/> there.</summary>
    public CursorWorktreeSetup? Unix { get; }

    /// <summary>Gets the <c>setup-worktree-windows</c> entry for Windows, or null. It takes precedence over <see cref="Generic"/> there.</summary>
    public CursorWorktreeSetup? Windows { get; }

    /// <summary>
    /// Looks for the file the way Cursor does: in the worktree, then in the project root.
    /// </summary>
    /// <param name="worktreePath">The new worktree.</param>
    /// <param name="projectRoot">The main checkout the worktree was made from.</param>
    /// <returns>The first file found, parsed, or null when neither has one.</returns>
    /// <exception cref="InvalidDataException">The file that was found is malformed. The other location is not tried.</exception>
    public static CursorWorktreesConfig? Find(string worktreePath, string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        foreach (var directory in new[] { worktreePath, projectRoot })
        {
            var candidate = Path.Combine(directory, ".cursor", "worktrees.json");
            if (File.Exists(candidate))
            {
                return Parse(File.ReadAllText(candidate), candidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the text of a <c>worktrees.json</c> file. Comments and trailing commas are accepted; keys other than the three setup keys are ignored.
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <param name="filePath">Where the file is. A script path is resolved against its directory.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="InvalidDataException">
    /// The text is not a JSON object, a setup value is neither a string nor an array of non-blank strings, or a script path is empty,
    /// absolute, or leaves the directory of the file.
    /// </exception>
    public static CursorWorktreesConfig Parse(string text, string filePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var full = Path.GetFullPath(filePath);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, ReadOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"'{full}' is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"'{full}' must hold a JSON object.");
            }

            var directory = Path.GetDirectoryName(full)!;
            return new CursorWorktreesConfig(
                full,
                Entry(document.RootElement, GenericKey, directory, full),
                Entry(document.RootElement, UnixKey, directory, full),
                Entry(document.RootElement, WindowsKey, directory, full));
        }
    }

    /// <summary>
    /// Chooses the entry Cursor would run on an operating system: the OS-specific key when present, else the generic key.
    /// </summary>
    /// <param name="windows">True for Windows, false for macOS and Linux.</param>
    /// <returns>The entry, or null when the file defines none for the system.</returns>
    public CursorWorktreeSetup? SetupFor(bool windows) => (windows ? Windows : Unix) ?? Generic;

    private static CursorWorktreeSetup? Entry(JsonElement root, string key, string directory, string file)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return Script(value.GetString()!, key, directory, file);
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"'{file}': '{key}' must be an array of shell commands or a script path.");
        }

        var commands = new List<string>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new InvalidDataException($"'{file}': every command in '{key}' must be a non-blank string.");
            }

            commands.Add(item.GetString()!);
        }

        return new CursorWorktreeSetup(commands, ScriptPath: null);
    }

    private static CursorWorktreeSetup Script(string relative, string key, string directory, string file)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{file}': '{key}' must be a script path relative to the file.");
        }

        var resolved = Path.GetFullPath(relative, directory);
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!resolved.StartsWith(prefix, comparison))
        {
            throw new InvalidDataException($"'{file}': the script of '{key}' leaves the directory of the file.");
        }

        return new CursorWorktreeSetup([], resolved);
    }
}
