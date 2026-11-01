#pragma warning disable MA0048 // The plan owns this file for the grant record, its scopes and the global store.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

/// <summary>Whether a trusted folder may be written.</summary>
public enum CoworkFolderAccess
{
    /// <summary>The folder may be read and not written.</summary>
    ReadOnly = 0,

    /// <summary>The folder may be read and written.</summary>
    ReadWrite = 1,
}

/// <summary>How long a folder grant lasts.</summary>
public enum CoworkFolderScope
{
    /// <summary>The grant lives on one session.</summary>
    Session = 0,

    /// <summary>The grant lives on the project and survives a new session of that project.</summary>
    Project = 1,

    /// <summary>The grant lives in the account-wide store.</summary>
    Global = 2,
}

/// <summary>A trusted folder with an access mode and a scope.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Access">Whether writes are allowed.</param>
/// <param name="Scope">Where the grant is stored.</param>
public sealed record CoworkFolderGrant(string Path, CoworkFolderAccess Access, CoworkFolderScope Scope);

/// <summary>
/// Account-wide folder grants. The file is <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/folder-grants.json</c>.
/// Writes are atomic and owner-only. Unknown JSON members are kept. A revoked grant applies to the next session.
/// </summary>
public sealed class CoworkFolderGrantStore
{
    /// <summary>The sentence shown when a grant change does not affect the session already running.</summary>
    public const string FolderChangeAppliesNextSession = "FolderChangeAppliesNextSession";

    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "folder-grants.json";

    /// <summary>The largest file that is read or written, in bytes.</summary>
    public const int MaxFileBytes = 256 * 1024;

    private readonly CoworkJsonFile<CoworkFolderGrantDocument> _file;

    /// <summary>Initializes a new instance of the <see cref="CoworkFolderGrantStore"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public CoworkFolderGrantStore(string stateRoot, string accountFolderKey)
    {
        _file = new CoworkJsonFile<CoworkFolderGrantDocument>(
            System.IO.Path.Combine(CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey), FileName),
            MaxFileBytes,
            static () => new CoworkFolderGrantDocument(),
            CoworkFolderGrantJsonContext.Default.CoworkFolderGrantDocument);
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath => _file.FilePath;

    /// <summary>Returns the global grants.</summary>
    /// <returns>The saved grants.</returns>
    public IReadOnlyList<CoworkFolderGrant> List()
    {
        var document = _file.Read();
        return document.Grants is null ? [] : [.. document.Grants.Select(ToGrant)];
    }

    /// <summary>Inserts or replaces a global grant. Unknown members are kept. The change applies to the next session.</summary>
    /// <param name="grant">The grant.</param>
    /// <returns><see cref="FolderChangeAppliesNextSession"/>.</returns>
    public string Grant(CoworkFolderGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (string.IsNullOrWhiteSpace(grant.Path))
        {
            throw new ArgumentException("The folder is required.", nameof(grant));
        }
        _file.Update(document =>
        {
            document.Grants ??= [];
            document.Grants.RemoveAll(entry => string.Equals(entry.Path, grant.Path, StringComparison.Ordinal));
            document.Grants.Add(new CoworkFolderGrantEntry
            {
                Path = grant.Path,
                Access = grant.Access.ToString(),
                Scope = CoworkFolderScope.Global.ToString(),
            });
        });
        return FolderChangeAppliesNextSession;
    }

    /// <summary>Removes a global grant. Unknown members are kept. The change applies to the next session.</summary>
    /// <param name="path">The folder.</param>
    /// <returns><see cref="FolderChangeAppliesNextSession"/>.</returns>
    public string Revoke(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _file.Update(document => document.Grants?.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.Ordinal)));
        return FolderChangeAppliesNextSession;
    }

    private static CoworkFolderGrant ToGrant(CoworkFolderGrantEntry entry) =>
        new(
            entry.Path ?? string.Empty,
            Enum.TryParse<CoworkFolderAccess>(entry.Access, out var access) ? access : CoworkFolderAccess.ReadOnly,
            CoworkFolderScope.Global);
}

/// <summary>The global grant file.</summary>
internal sealed class CoworkFolderGrantDocument
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the grants.</summary>
    public List<CoworkFolderGrantEntry>? Grants { get; set; }

    /// <summary>Gets or sets unknown members.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>One grant in the global file.</summary>
internal sealed class CoworkFolderGrantEntry
{
    /// <summary>Gets or sets the folder.</summary>
    public string? Path { get; set; }

    /// <summary>Gets or sets the access mode.</summary>
    public string? Access { get; set; }

    /// <summary>Gets or sets the scope.</summary>
    public string? Scope { get; set; }
}

/// <summary>Source-generated JSON for the global grant file.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CoworkFolderGrantDocument))]
internal sealed partial class CoworkFolderGrantJsonContext : JsonSerializerContext
{
}

/// <summary>Builds grant JSON without reflection.</summary>
internal static class CoworkFolderGrantJson
{
    /// <summary>Serialises grants to a JSON element.</summary>
    /// <param name="grants">The grants.</param>
    /// <param name="scope">The scope written into each object.</param>
    /// <returns>The element.</returns>
    public static JsonElement Element(IReadOnlyList<CoworkFolderGrant> grants, CoworkFolderScope scope)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append('[');
        for (var index = 0; index < grants.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var item = grants[index];
            builder.Append("{\"path\":").Append(Quote(item.Path));
            builder.Append(",\"access\":").Append(Quote(item.Access.ToString()));
            builder.Append(",\"scope\":").Append(Quote(scope.ToString()));
            builder.Append('}');
        }

        builder.Append(']');
        using var document = JsonDocument.Parse(builder.ToString());
        return document.RootElement.Clone();
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
