using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OmpOS.Workspace.Sets;

/// <summary>
/// A VS Code or Cursor <c>.code-workspace</c> file. Reading accepts JSON with comments and trailing commas. Folder paths that are
/// relative are resolved against the directory of the file. Everything the file holds besides <c>folders</c> (settings,
/// extension recommendations, launch and task blocks, and any key of a folder other than <c>path</c> and <c>name</c>) is kept
/// as it was read, so exporting a changed set into the file it came from does not lose it. Comments are tolerated on input and are
/// not written back, because the JSON document model does not carry them. Folders given as a <c>uri</c> (for example
/// <c>vscode-remote://</c>) are not roots: import skips them with a warning and export keeps them after the roots.
/// </summary>
public sealed class CodeWorkspaceFile
{
    /// <summary>The file extension of a workspace file, including the dot.</summary>
    public const string Extension = ".code-workspace";

    private const string FoldersKey = "folders";
    private const string PathKey = "path";
    private const string NameKey = "name";
    private const string UriKey = "uri";
    private const int MaxRelativeClimb = 2;

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly JsonObject _document;

    private CodeWorkspaceFile(string filePath, JsonObject document)
    {
        FilePath = filePath;
        _document = document;
    }

    /// <summary>Gets the absolute path of the file, the base of its relative folder paths.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Reads a workspace file from disk.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The content is not a JSON object, or <c>folders</c> is present but not an array of objects.</exception>
    public static CodeWorkspaceFile Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Parse(File.ReadAllText(filePath), filePath);
    }

    /// <summary>
    /// Parses workspace file text.
    /// </summary>
    /// <param name="text">The file content: JSON, possibly with comments and trailing commas.</param>
    /// <param name="filePath">Where the file is or will be. Relative folder paths are resolved against its directory.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="InvalidDataException">The content is not a JSON object, or <c>folders</c> is present but not an array of objects.</exception>
    public static CodeWorkspaceFile Parse(string text, string filePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, nodeOptions: null, ReadOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"'{filePath}' is not a valid workspace file: {exception.Message}", exception);
        }

        if (node is not JsonObject document)
        {
            throw new InvalidDataException($"'{filePath}' is not a valid workspace file: the top level must be a JSON object.");
        }

        if (document.TryGetPropertyValue(FoldersKey, out var folders)
            && (folders is not JsonArray array || array.Any(entry => entry is not JsonObject)))
        {
            throw new InvalidDataException($"'{filePath}' is not a valid workspace file: 'folders' must be an array of objects.");
        }

        return new CodeWorkspaceFile(Path.GetFullPath(filePath), document);
    }

    /// <summary>
    /// Builds a file that has only the folders of a set.
    /// </summary>
    /// <param name="set">The set to export.</param>
    /// <param name="filePath">Where the file will be written.</param>
    /// <returns>The file, not yet written.</returns>
    /// <exception cref="InvalidOperationException">A root of the set belongs to a remote environment.</exception>
    public static CodeWorkspaceFile Create(WorkspaceSet set, string filePath)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new CodeWorkspaceFile(Path.GetFullPath(filePath), new JsonObject()).WithSet(set);
    }

    /// <summary>
    /// Reads the folders of the file as a set. The first folder is the primary root; the name of a folder is its <c>name</c> or its last path segment.
    /// </summary>
    /// <param name="environmentId">The environment that owns the folders, or null for this machine.</param>
    /// <returns>The set and the entries that were skipped.</returns>
    /// <exception cref="InvalidDataException">The file has no folder with a <c>path</c>.</exception>
    public CodeWorkspaceImport ToWorkspaceSet(string? environmentId = null)
    {
        var directory = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var warnings = new List<string>();
        WorkspaceSet? set = null;
        var name = Path.GetFileNameWithoutExtension(FilePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "workspace";
        }

        var position = 0;
        foreach (var entry in FolderObjects())
        {
            position++;
            if (PathOf(entry) is not { } rawPath)
            {
                warnings.Add(entry.ContainsKey(UriKey)
                    ? $"Folder {position} is given as a uri and was skipped: only folders on this machine can be roots."
                    : $"Folder {position} has no path and was skipped.");
                continue;
            }

            var absolute = TrimSeparators(Path.GetFullPath(rawPath, directory));
            var displayName = entry[NameKey] is JsonValue nameValue && nameValue.TryGetValue<string>(out var given) && !string.IsNullOrWhiteSpace(given)
                ? given
                : null;
            set = set is null
                ? new WorkspaceSet(
                    WorkspaceRootNaming.Slug(name),
                    name,
                    [new WorkspaceSetRoot(WorkspaceRootNaming.Slug(displayName ?? WorkspaceRootNaming.DefaultName(absolute)), absolute, displayName ?? WorkspaceRootNaming.DefaultName(absolute), environmentId, IsPrimary: true)])
                : set.AddFolder(absolute, displayName);
        }

        return set is null
            ? throw new InvalidDataException($"'{FilePath}' has no folder with a path.")
            : new CodeWorkspaceImport(set, warnings);
    }

    /// <summary>
    /// Returns a copy of this file whose folders are the roots of a set, in order. Every other key of the document, and every
    /// extra key of a folder that stays, is kept; <c>uri</c> folders of this file stay after the roots. Paths are written relative to the
    /// file when the root is at most two levels above it, and absolute otherwise.
    /// </summary>
    /// <param name="set">The set to export.</param>
    /// <returns>The new file; this file is unchanged.</returns>
    /// <exception cref="InvalidOperationException">A root of the set belongs to a remote environment.</exception>
    public CodeWorkspaceFile WithSet(WorkspaceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var remote = set.Roots.FirstOrDefault(root => root.Kind == WorkspaceRootKind.Remote);
        if (remote is not null)
        {
            throw new InvalidOperationException($"'{remote.DisplayName}' lives in environment '{remote.EnvironmentId}'. A .code-workspace file describes folders on this machine only.");
        }

        var directory = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var known = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var candidate in FolderObjects())
        {
            if (PathOf(candidate) is { } existing)
            {
                known.TryAdd(TrimSeparators(Path.GetFullPath(existing, directory)), candidate);
            }
        }

        var folders = new JsonArray();
        foreach (var root in set.Roots)
        {
            var node = known.TryGetValue(TrimSeparators(Path.GetFullPath(root.Path)), out var kept)
                ? (JsonObject)kept.DeepClone()
                : new JsonObject();
            node[PathKey] = JsonValue.Create(Express(root.Path, directory));
            if (node.ContainsKey(NameKey) || !string.Equals(root.DisplayName, WorkspaceRootNaming.DefaultName(root.Path), StringComparison.Ordinal))
            {
                node[NameKey] = JsonValue.Create(root.DisplayName);
            }

            folders.Add(node);
        }

        foreach (var skipped in FolderObjects().Where(candidate => PathOf(candidate) is null))
        {
            folders.Add(skipped.DeepClone());
        }

        var document = (JsonObject)_document.DeepClone();
        document[FoldersKey] = folders;
        return new CodeWorkspaceFile(FilePath, document);
    }

    /// <summary>
    /// Renders the file as indented JSON with tabs, the way VS Code writes it, without a byte-order mark.
    /// </summary>
    /// <returns>The JSON text, ending with a newline.</returns>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        var options = new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = '\t',
            IndentSize = 1,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            _document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length) + "\n";
    }

    /// <summary>
    /// Writes the file to <see cref="FilePath"/> through a temporary file in the same directory, so a failure leaves the old content.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Write()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{FilePath}.{Guid.NewGuid().ToString("N")}.tmp";
        try
        {
            File.WriteAllText(temporary, ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string? PathOf(JsonObject folder) =>
        folder[PathKey] is JsonValue value && value.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path) ? path : null;

    private static string TrimSeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        return path.Length > (root?.Length ?? 0) ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
    }

    private static string Express(string absolutePath, string fileDirectory)
    {
        var relative = Path.GetRelativePath(fileDirectory, absolutePath).Replace('\\', '/');
        if (Path.IsPathRooted(relative))
        {
            return absolutePath.Replace('\\', '/');
        }

        var climbs = relative.Split('/').Count(segment => segment == "..");
        return climbs > MaxRelativeClimb ? absolutePath.Replace('\\', '/') : relative;
    }

    private IEnumerable<JsonObject> FolderObjects() =>
        _document.TryGetPropertyValue(FoldersKey, out var node) && node is JsonArray array
            ? array.OfType<JsonObject>()
            : [];
}
