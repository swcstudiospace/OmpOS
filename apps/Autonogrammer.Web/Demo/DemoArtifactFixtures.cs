using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Web.Services.Artifacts;

namespace Autonogrammer.Web.Demo;

/// <summary>
/// The Demo's artifacts: a Markdown note and a CSV table whose content is stored, and a Html artifact whose content was never stored
/// (metadata-only sync), so the Demo shows "sync is off" for it. The Demo serves no render document: an interactive view is answered
/// with the static view by the client. Every body is in the shape of the API's artifact documents.
/// </summary>
internal static class DemoArtifactFixtures
{
    /// <summary>The Markdown note.</summary>
    internal const string NotesId = "artifact-demo-notes";

    /// <summary>The CSV table. Its formula-like cell is shown with its mark, never evaluated.</summary>
    internal const string TableId = "artifact-demo-table";

    /// <summary>The metadata-only artifact: its header is listed, its content is not stored.</summary>
    internal const string MetadataOnlyId = "artifact-demo-metadata";

    private static readonly byte[] NotesBytes = Encoding.UTF8.GetBytes("# Demo notes\n\nA short Markdown artifact. It runs no script.\n");
    private static readonly byte[] TableBytes = Encoding.UTF8.GetBytes("item,count\nApples,3\n=HYPERLINK(\"https://example.invalid\"),1\n");

    private static readonly (string Id, string Title, string Kind, byte[]? Content)[] Items =
    [
        (NotesId, "Demo notes", "Markdown", NotesBytes),
        (TableId, "Demo table", "Csv", TableBytes),
        (MetadataOnlyId, "Metadata only", "Html", null),
    ];

    /// <summary>Gets the content media type of a stored version.</summary>
    internal const string ContentMediaType = "application/octet-stream";

    /// <summary>The artifact list the Demo answers with.</summary>
    /// <returns>Every fixture header.</returns>
    internal static ArtifactListRecord List() => new(Items.Select(Header).ToList());

    /// <summary>One header, or null when the identifier is not a fixture.</summary>
    /// <param name="id">The artifact identifier.</param>
    /// <returns>The header.</returns>
    internal static ArtifactSummaryRecord? Find(string id)
    {
        foreach (var item in Items)
        {
            if (string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                return Header(item);
            }
        }

        return null;
    }

    /// <summary>The stored bytes of version 1, or null when the version has no stored content.</summary>
    /// <param name="id">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <returns>The bytes, or null.</returns>
    internal static byte[]? Content(string id, int number)
    {
        if (number != 1)
        {
            return null;
        }

        foreach (var item in Items)
        {
            if (string.Equals(item.Id, id, StringComparison.Ordinal))
            {
                return item.Content;
            }
        }

        return null;
    }

    private static ArtifactSummaryRecord Header((string Id, string Title, string Kind, byte[]? Content) item) =>
        new(item.Id, item.Title, item.Kind, 1, Convert.ToHexStringLower(SHA256.HashData(item.Content ?? Encoding.UTF8.GetBytes(item.Id))));
}
