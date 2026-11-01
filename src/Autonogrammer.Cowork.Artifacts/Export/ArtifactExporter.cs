using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts.Export;

/// <summary>
/// Writes one version as a file or an artifact as a deterministic zip. There is no import path, so there is no zip-slip surface.
/// </summary>
public sealed class ArtifactExporter
{
    /// <summary>The panel meta policy placed first in a safe HTML export.</summary>
    public const string PanelMetaPolicy = """<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src data:; sandbox">""";

    private readonly ArtifactOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ArtifactExporter"/> class.</summary>
    /// <param name="options">Limits used as the uncompressed zip cap.</param>
    public ArtifactExporter(ArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }


    /// <summary>Exports one verified version as a file.</summary>
    /// <param name="snapshot">The verified version.</param>
    /// <param name="choice">Safe or original.</param>
    /// <returns>The file bytes and sanitised name.</returns>
    public static ArtifactExport ExportFile(ArtifactSnapshot snapshot, ArtifactExportChoice choice)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var bytes = Apply(snapshot, choice);
        return new ArtifactExport
        {
            Bytes = bytes,
            FileName = FileName(snapshot.Title, snapshot.Kind, snapshot.Number),
            RiskNote = choice == ArtifactExportChoice.Original ? ArtifactExport.OriginalRiskNote : null,
        };
    }

    /// <summary>
    /// Exports every version as a zip: <c>manifest.json</c>, then <c>v0001/name.ext</c>. Entry timestamps come from
    /// <see cref="ArtifactSnapshot.CreatedAt"/> so two exports hash equal.
    /// </summary>
    /// <param name="versions">Verified versions of one artifact, in number order.</param>
    /// <param name="choice">Safe or original.</param>
    /// <returns>The zip bytes.</returns>
    /// <exception cref="InvalidOperationException">The uncompressed total exceeds the account cap.</exception>
    public ArtifactExport ExportZip(IReadOnlyList<ArtifactSnapshot> versions, ArtifactExportChoice choice)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            throw new ArgumentException("At least one version is required.", nameof(versions));
        }

        var prepared = new List<byte[]>(versions.Count);
        long total = 0;
        foreach (var version in versions)
        {
            var bytes = Apply(version, choice);
            total += bytes.Length;
            prepared.Add(bytes);
        }

        if (total > _options.MaxAccountBytes)
        {
            throw new InvalidOperationException("Uncompressed export exceeds Cowork:Artifacts:MaxAccountBytes.");
        }

        var names = new List<string>(versions.Count);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in versions)
        {
            names.Add(UniqueName(FileName(version.Title, version.Kind, version.Number), used));
        }

        var manifestBytes = ManifestBytes(versions, names, choice);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "manifest.json", manifestBytes, versions[0].CreatedAt);
            for (var index = 0; index < versions.Count; index++)
            {
                var folder = "v" + versions[index].Number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                WriteEntry(zip, folder + "/" + names[index], prepared[index], versions[index].CreatedAt);
            }
        }

        return new ArtifactExport
        {
            Bytes = buffer.ToArray(),
            FileName = SanitizeStem(versions[0].Title) + ".zip",
            RiskNote = choice == ArtifactExportChoice.Original ? ArtifactExport.OriginalRiskNote : null,
        };
    }

    /// <summary>SHA-256 of export bytes, lowercase hex.</summary>
    /// <param name="export">The export.</param>
    /// <returns>The hash.</returns>
    public static string Hash(ArtifactExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        return Convert.ToHexStringLower(SHA256.HashData(export.Bytes.Span));
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] bytes, DateTimeOffset createdAt)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = createdAt.ToUniversalTime();
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static byte[] Apply(ArtifactSnapshot snapshot, ArtifactExportChoice choice)
    {
        if (choice == ArtifactExportChoice.Original)
        {
            return snapshot.Content.ToArray();
        }

        return snapshot.Kind switch
        {
            ArtifactKind.Svg => Encoding.UTF8.GetBytes(SanitizeSvg(Encoding.UTF8.GetString(snapshot.Content.Span))),
            ArtifactKind.Csv => Encoding.UTF8.GetBytes(NeutralizeCsv(Encoding.UTF8.GetString(snapshot.Content.Span))),
            ArtifactKind.Html => Encoding.UTF8.GetBytes(WrapHtml(Encoding.UTF8.GetString(snapshot.Content.Span))),
            _ => snapshot.Content.ToArray(),
        };
    }

    internal static string SanitizeSvg(string document)
    {
        var withoutDoctype = DropDoctype(document);
        var builder = new StringBuilder(withoutDoctype.Length);
        var index = 0;
        while (index < withoutDoctype.Length)
        {
            if (StartsWithIgnoreCase(withoutDoctype, index, "<script"))
            {
                var close = IndexOfIgnoreCase(withoutDoctype, "</script>", index);
                index = close < 0 ? withoutDoctype.Length : close + "</script>".Length;
                continue;
            }

            if (withoutDoctype[index] == '<' && !StartsWithIgnoreCase(withoutDoctype, index, "</"))
            {
                var end = withoutDoctype.IndexOf('>', index);
                if (end < 0)
                {
                    _ = builder.Append(withoutDoctype.AsSpan(index));
                    break;
                }

                _ = builder.Append(StripEventAttributes(withoutDoctype[index..(end + 1)]));
                index = end + 1;
                continue;
            }

            _ = builder.Append(withoutDoctype[index]);
            index++;
        }

        return builder.ToString();
    }

    internal static string WrapHtml(string document)
    {
        var body = DropDoctype(document.TrimStart());
        return PanelMetaPolicy + "\n" + body;
    }

    internal static string NeutralizeCsv(string document)
    {
        var builder = new StringBuilder(document.Length);
        var index = 0;
        var atFieldStart = true;
        while (index < document.Length)
        {
            var character = document[index];
            if (atFieldStart && character is '=' or '+' or '-' or '@')
            {
                _ = builder.Append('\'');
            }

            _ = builder.Append(character);
            if (character == '"')
            {
                var end = index + 1;
                while (end < document.Length)
                {
                    _ = builder.Append(document[end]);
                    if (document[end] == '"')
                    {
                        if (end + 1 < document.Length && document[end + 1] == '"')
                        {
                            end++;
                            _ = builder.Append(document[end]);
                        }
                        else
                        {
                            end++;
                            break;
                        }
                    }

                    end++;
                }

                index = end;
                atFieldStart = false;
                continue;
            }

            atFieldStart = character is ',' or '\n';
            index++;
        }

        return builder.ToString();
    }

    private static string DropDoctype(string document)
    {
        var trimmed = document.TrimStart();
        if (!StartsWithIgnoreCase(trimmed, 0, "<!DOCTYPE"))
        {
            return document;
        }

        var end = trimmed.IndexOf('>', StringComparison.Ordinal);
        if (end < 0)
        {
            return document;
        }

        return trimmed[(end + 1)..].TrimStart();
    }

    private static string StripEventAttributes(string tag)
    {
        var builder = new StringBuilder(tag.Length);
        var index = 0;
        while (index < tag.Length)
        {
            if (IsOnAttribute(tag, index))
            {
                index = SkipAttribute(tag, index);
                continue;
            }

            if (IsExternalReference(tag, index))
            {
                index = SkipAttribute(tag, index);
                continue;
            }

            _ = builder.Append(tag[index]);
            index++;
        }

        return builder.ToString();
    }

    private static bool IsOnAttribute(string tag, int index) =>
        (index == 0 || char.IsWhiteSpace(tag[index - 1]))
        && index + 2 < tag.Length
        && (tag[index] is 'o' or 'O')
        && (tag[index + 1] is 'n' or 'N')
        && char.IsLetter(tag[index + 2]);

    private static bool IsExternalReference(string tag, int index)
    {
        if (index > 0 && !char.IsWhiteSpace(tag[index - 1]))
        {
            return false;
        }

        return StartsWithIgnoreCase(tag, index, "href") || StartsWithIgnoreCase(tag, index, "xlink:href");
    }

    private static int SkipAttribute(string tag, int index)
    {
        while (index < tag.Length && tag[index] is not '=' and not '>' and not '/' && !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        while (index < tag.Length && char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        if (index >= tag.Length || tag[index] != '=')
        {
            return index;
        }

        index++;
        while (index < tag.Length && char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        if (index >= tag.Length)
        {
            return index;
        }

        if (tag[index] is '"' or '\'')
        {
            var quote = tag[index];
            index++;
            while (index < tag.Length && tag[index] != quote)
            {
                index++;
            }

            return index < tag.Length ? index + 1 : index;
        }

        while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] is not '>' and not '/')
        {
            index++;
        }

        return index;
    }

    internal static string FileName(string title, ArtifactKind kind, int number)
    {
        var stem = SanitizeStem(title);
        var extension = Extension(kind);
        var full = stem + extension;
        if (full.Length <= 80)
        {
            return full;
        }

        var room = Math.Max(1, 80 - extension.Length);
        return stem[..room] + extension;
    }

    internal static string SanitizeStem(string title)
    {
        var builder = new StringBuilder(Math.Min(title.Length, 80));
        foreach (var character in title)
        {
            if (builder.Length == 80)
            {
                break;
            }

            if (character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-' or ' ')
            {
                _ = builder.Append(character);
            }
        }

        var stem = builder.ToString().Trim();
        if (stem.Length == 0)
        {
            stem = "artifact";
        }

        var dot = stem.IndexOf('.', StringComparison.Ordinal);
        var bare = dot < 0 ? stem : stem[..dot];
        if (IsReserved(bare))
        {
            stem = "_" + stem;
        }

        return stem.Length > 80 ? stem[..80] : stem;
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        var stem = dot < 0 ? name : name[..dot];
        var extension = dot < 0 ? string.Empty : name[dot..];
        var suffix = 2;
        while (true)
        {
            var marker = "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var room = Math.Max(1, 80 - extension.Length - marker.Length);
            var candidate = stem[..Math.Min(stem.Length, room)] + marker + extension;
            if (used.Add(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static bool IsReserved(string bare)
    {
        if (bare.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (bare.Length != 4)
        {
            return false;
        }

        var prefix = bare[..3];
        var digit = bare[3];
        return digit is >= '1' and <= '9'
            && (prefix.Equals("COM", StringComparison.OrdinalIgnoreCase) || prefix.Equals("LPT", StringComparison.OrdinalIgnoreCase));
    }

    private static string Extension(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Markdown => ".md",
        ArtifactKind.Code => ".txt",
        ArtifactKind.Html => ".html",
        ArtifactKind.Svg => ".svg",
        ArtifactKind.Mermaid => ".mmd",
        ArtifactKind.Csv => ".csv",
        ArtifactKind.Image => ".img",
        _ => ".bin",
    };

    private static bool StartsWithIgnoreCase(string value, int index, string prefix) =>
        index + prefix.Length <= value.Length && value.AsSpan(index, prefix.Length).Equals(prefix, StringComparison.OrdinalIgnoreCase);

    private static int IndexOfIgnoreCase(string value, string needle, int start)
    {
        for (var index = start; index + needle.Length <= value.Length; index++)
        {
            if (value.AsSpan(index, needle.Length).Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static byte[] ManifestBytes(IReadOnlyList<ArtifactSnapshot> versions, List<string> names, ArtifactExportChoice choice)
    {
        var builder = new StringBuilder();
        _ = builder.Append("{\"artifactId\":");
        AppendJson(builder, versions[0].ArtifactId);
        _ = builder.Append(",\"choice\":");
        AppendJson(builder, choice.ToString());
        _ = builder.Append(",\"riskNote\":");
        if (choice == ArtifactExportChoice.Original)
        {
            AppendJson(builder, ArtifactExport.OriginalRiskNote);
        }
        else
        {
            _ = builder.Append("null");
        }

        _ = builder.Append(",\"versions\":[");
        for (var index = 0; index < versions.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(',');
            }

            var version = versions[index];
            var name = "v" + version.Number.ToString("D4", CultureInfo.InvariantCulture) + "/" + names[index];
            _ = builder.Append("{\"number\":");
            _ = builder.Append(version.Number.ToString(CultureInfo.InvariantCulture));
            _ = builder.Append(",\"name\":");
            AppendJson(builder, name);
            _ = builder.Append(",\"contentSha256\":");
            AppendJson(builder, version.ContentSha256);
            _ = builder.Append(",\"createdAt\":");
            AppendJson(builder, version.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            _ = builder.Append('}');
        }

        _ = builder.Append("]}");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendJson(StringBuilder builder, string value)
    {
        _ = builder.Append('"');
        foreach (var character in value)
        {
            if (character is '"' or '\\')
            {
                _ = builder.Append('\\');
            }

            _ = builder.Append(character);
        }

        _ = builder.Append('"');
    }
}
