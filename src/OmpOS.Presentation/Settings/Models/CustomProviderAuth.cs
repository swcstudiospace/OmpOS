namespace OmpOS.Presentation.Settings.Models;

/// <summary>How a custom provider's endpoint expects the client to authenticate.</summary>
public enum CustomProviderAuth
{
    /// <summary>No credential.</summary>
    None = 0,

    /// <summary>An API key.</summary>
    ApiKey = 1,

    /// <summary>An API key sent as a Bearer token.</summary>
    Bearer = 2,
}
