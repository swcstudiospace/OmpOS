using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Providers;

/// <summary>How an endpoint expects the key. The catalogue records the scheme only, never a key value.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogueAuthStyle>))]
public enum CatalogueAuthStyle
{
    /// <summary>The key is sent as <c>Authorization: Bearer &lt;key&gt;</c>.</summary>
    Bearer = 0,

    /// <summary>The key is sent in the <c>x-api-key</c> header, the Anthropic Messages convention.</summary>
    XApiKey,

    /// <summary>The key is sent in the header named by <see cref="CatalogueRoute.AuthHeader"/>.</summary>
    CustomHeader,

    /// <summary>The endpoint needs no key.</summary>
    None,
}
