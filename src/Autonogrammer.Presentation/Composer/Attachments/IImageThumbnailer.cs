namespace Autonogrammer.Presentation.Composer.Attachments;

/// <summary>Produces a small preview of an image. The UI framework implements it; the model never decodes images itself.</summary>
public interface IImageThumbnailer
{
    /// <summary>Creates a thumbnail whose longest edge is at most <paramref name="maxEdge"/> pixels.</summary>
    /// <param name="data">The encoded image.</param>
    /// <param name="mimeType">The validated MIME type of <paramref name="data"/>.</param>
    /// <param name="maxEdge">The largest edge in pixels.</param>
    /// <returns>The encoded thumbnail, or <see langword="null"/> when the image cannot be decoded; the attachment is then shown without a preview.</returns>
    byte[]? CreateThumbnail(byte[] data, string mimeType, int maxEdge);
}
