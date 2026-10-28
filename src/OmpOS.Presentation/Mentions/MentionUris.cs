namespace OmpOS.Presentation.Mentions;

/// <summary>Builds the resource identifiers of mentions that are not files.</summary>
internal static class MentionUris
{
    /// <summary>Builds an <c>ompos:</c> URI such as <c>ompos://terminal/abc</c>.</summary>
    /// <param name="area">The kind of thing: <c>terminal</c>, <c>chat</c>, <c>git</c>, <c>browser</c>, <c>rule</c>, <c>skill</c> or <c>pr</c>.</param>
    /// <param name="id">The identifier inside the area.</param>
    /// <returns>The URI.</returns>
    internal static Uri Custom(string area, string id) => new("ompos://" + area + "/" + Uri.EscapeDataString(id), UriKind.Absolute);
}
