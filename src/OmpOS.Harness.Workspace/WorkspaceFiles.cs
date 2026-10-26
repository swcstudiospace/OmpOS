using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Workspace;

/// <summary>
/// Reads convention files through the workspace root so that no read leaves the root.
/// </summary>
internal static class WorkspaceFiles
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly byte[] Utf8ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Converts a relative path to the portable form used in diagnostics and results: '/' separators.
    /// </summary>
    public static string ToPortable(string relativePath) =>
        relativePath.Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Reads a file as strict UTF-8 text. A UTF-8 byte order mark is removed.
    /// </summary>
    /// <returns>The text, or the reason the file is malformed.</returns>
    public static (string? Text, string? Reason) ReadText(WorkspaceRoot root, string relativePath)
    {
        var (text, _, reason) = ReadTextWithHash(root, relativePath);
        return (text, reason);
    }

    /// <summary>
    /// Reads a file as strict UTF-8 text and hashes the same bytes, so the hash always describes the text that was returned.
    /// </summary>
    /// <returns>
    /// The text and the lowercase hexadecimal SHA-256 of the raw file bytes (including any byte order mark), or the reason
    /// the file is malformed.
    /// </returns>
    public static (string? Text, string? Sha256, string? Reason) ReadTextWithHash(WorkspaceRoot root, string relativePath)
    {
        string fullPath;
        try
        {
            fullPath = root.Resolve(relativePath);
        }
        catch (WorkspacePathException exception)
        {
            return (null, null, exception.Reason);
        }

        try
        {
            var length = new FileInfo(fullPath).Length;
            if (length > WorkspaceConventions.MaxConventionFileBytes)
            {
                return (null, null, $"the file is {length} bytes, above the {WorkspaceConventions.MaxConventionFileBytes} byte limit");
            }

            var bytes = File.ReadAllBytes(fullPath);
            var content = bytes.AsSpan();
            if (content.StartsWith(Utf8ByteOrderMark))
            {
                content = content[Utf8ByteOrderMark.Length..];
            }

            var text = StrictUtf8.GetString(content);
            return text.Trim().Length == 0
                ? (null, null, "the file is empty")
                : (text, Convert.ToHexStringLower(SHA256.HashData(bytes)), null);
        }
        catch (DecoderFallbackException)
        {
            return (null, null, "the file is not valid UTF-8 text");
        }
        catch (IOException exception)
        {
            return (null, null, $"the file cannot be read: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return (null, null, $"the file cannot be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Lists the files of one convention folder that have the given extension, in ordinal name order.
    /// Subfolders are not searched. Entries that are not files with that extension are ignored.
    /// </summary>
    /// <returns>Portable relative paths such as <c>.ompos/rules/style.md</c>.</returns>
    public static IReadOnlyList<string> EnumerateConventionFiles(
        WorkspaceRoot root,
        string folder,
        string extension,
        List<WorkspaceDiagnostic> diagnostics)
    {
        string folderPath;
        try
        {
            folderPath = root.Resolve(folder);
        }
        catch (WorkspacePathException exception)
        {
            diagnostics.Add(new WorkspaceDiagnostic(folder, exception.Reason));
            return [];
        }

        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseSensitive,
            ReturnSpecialDirectories = false,
        };

        try
        {
            var names = new List<string>();
            foreach (var file in Directory.EnumerateFiles(folderPath, "*" + extension, options))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(extension, StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names.ConvertAll(name => folder + "/" + name);
        }
        catch (IOException exception)
        {
            diagnostics.Add(new WorkspaceDiagnostic(folder, $"the folder cannot be read: {exception.Message}"));
            return [];
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new WorkspaceDiagnostic(folder, $"the folder cannot be read: {exception.Message}"));
            return [];
        }
    }
}
