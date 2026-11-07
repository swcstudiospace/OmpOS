using Autonogrammer.Presentation.Cowork.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// The host document for one surface request, or the reason no document can be shown. A document always starts with the
/// content-security-policy meta element (D-20).
/// </summary>
/// <param name="Document">The complete document, or null when it cannot be shown.</param>
/// <param name="Reason">The reason when <paramref name="Document"/> is null.</param>
public sealed record ArtifactDocumentResult(string? Document, ArtifactSurfaceReason Reason)
{
    /// <summary>Creates a result that carries a document.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public static ArtifactDocumentResult Ready(string document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new ArtifactDocumentResult(document, ArtifactSurfaceReason.None);
    }

    /// <summary>Creates a result that carries no document.</summary>
    /// <param name="reason">Why no document can be shown.</param>
    /// <returns>The result.</returns>
    public static ArtifactDocumentResult Refused(ArtifactSurfaceReason reason) => new(null, reason);
}
