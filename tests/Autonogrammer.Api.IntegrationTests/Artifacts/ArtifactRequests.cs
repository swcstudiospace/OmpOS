using System.Net.Http.Headers;
using System.Text;
using Autonogrammer.Cowork.Artifacts.Stores;

namespace Autonogrammer.Api.IntegrationTests.Artifacts;

/// <summary>Request bodies the artifact route tests share.</summary>
internal static class ArtifactRequests
{
    /// <summary>Builds an octet-stream version body.</summary>
    /// <param name="text">The text.</param>
    /// <param name="kind">The kind header.</param>
    /// <returns>The content.</returns>
    public static ByteArrayContent Body(string text, string kind) => Raw(Encoding.UTF8.GetBytes(text), kind);

    /// <summary>Builds an octet-stream version body from raw bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="kind">The kind header.</param>
    /// <returns>The content.</returns>
    public static ByteArrayContent Raw(byte[] bytes, string kind)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(ArtifactWireContract.MediaType);
        content.Headers.Add(ArtifactWireContract.KindHeader, kind);
        content.Headers.Add(ArtifactWireContract.ContentSha256Header, ArtifactWireContract.Hash(bytes));
        return content;
    }
}
