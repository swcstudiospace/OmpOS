namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Wire names and the command-capability rule for <see cref="DispatchCapability"/>.</summary>
public static class DispatchCapabilityWire
{
    /// <summary>Parses one closed capability name.</summary>
    /// <param name="text">The wire value.</param>
    /// <param name="capability">The capability when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is one of the five closed names.</returns>
    public static bool TryParse(string? text, out DispatchCapability capability)
    {
        switch (text)
        {
            case "sessions":
                capability = DispatchCapability.Sessions;
                return true;
            case "code":
                capability = DispatchCapability.Code;
                return true;
            case "cowork":
                capability = DispatchCapability.Cowork;
                return true;
            case "browser_use":
                capability = DispatchCapability.BrowserUse;
                return true;
            case "computer_use":
                capability = DispatchCapability.ComputerUse;
                return true;
            default:
                capability = default;
                return false;
        }
    }

    /// <summary>Returns the wire name of a capability.</summary>
    /// <param name="capability">The capability.</param>
    /// <returns>The exact protocol string.</returns>
    public static string ToWire(DispatchCapability capability) => capability switch
    {
        DispatchCapability.Sessions => "sessions",
        DispatchCapability.Code => "code",
        DispatchCapability.Cowork => "cowork",
        DispatchCapability.BrowserUse => "browser_use",
        DispatchCapability.ComputerUse => "computer_use",
        _ => throw new ArgumentOutOfRangeException(nameof(capability)),
    };

    /// <summary>Reports whether a command may name this capability. Tool-class flags cannot.</summary>
    /// <param name="capability">The capability.</param>
    /// <returns><see langword="true"/> for <c>sessions</c>, <c>code</c> and <c>cowork</c> only.</returns>
    public static bool IsCommandCapability(DispatchCapability capability) =>
        capability is DispatchCapability.Sessions or DispatchCapability.Code or DispatchCapability.Cowork;
}
