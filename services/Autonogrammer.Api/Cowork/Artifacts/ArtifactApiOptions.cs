namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>API-only artifact settings bound from <c>Cowork:Artifacts</c>. Off by default.</summary>
internal sealed class ArtifactApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Artifacts";

    /// <summary>Gets or sets whether the routes serve artifacts. False answers <c>cowork_disabled</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the two render-ticket HMAC keys. Checked only when <see cref="Enabled"/> is true.</summary>
    public List<string> RenderTicketKeys { get; set; } = [];
}
