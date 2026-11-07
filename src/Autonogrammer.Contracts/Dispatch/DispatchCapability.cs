using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Closed capability identifiers. <see cref="BrowserUse"/> and <see cref="ComputerUse"/> describe tool classes on a desktop.
/// They are never a valid command capability.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DispatchCapability>))]
public enum DispatchCapability
{
    /// <summary>Session list, steer, cancel and approval decisions.</summary>
    [JsonStringEnumMemberName("sessions")]
    Sessions,

    /// <summary>Code prompts.</summary>
    [JsonStringEnumMemberName("code")]
    Code,

    /// <summary>Cowork tasks.</summary>
    [JsonStringEnumMemberName("cowork")]
    Cowork,

    /// <summary>Whether a dispatched cowork task may use Browser Use. Never a command capability.</summary>
    [JsonStringEnumMemberName("browser_use")]
    BrowserUse,

    /// <summary>Whether a dispatched cowork task may use Computer Use. Never a command capability.</summary>
    [JsonStringEnumMemberName("computer_use")]
    ComputerUse,
}
