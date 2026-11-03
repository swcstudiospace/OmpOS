using System.Globalization;
using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Turns the link failures the transport reports into the desktop's decision. The transport is unchanged and exposes no close frame
/// or response headers, so the close status comes from the text of <c>DisconnectedHarnessException</c> and the HTTP status of a refused
/// upgrade from the text of <c>ProcessStartHarnessException</c>. Missing or unparsable text is a network error.
/// </summary>
public static partial class DispatchCloseStatus
{
    /// <summary>The wait after a replaced connection (close 4009) and the floor of a rate-limited upgrade (HTTP 429).</summary>
    public static readonly TimeSpan SixtySeconds = TimeSpan.FromSeconds(60);

    /// <summary>Parses the close status from the disconnected-transport message.</summary>
    /// <param name="message">The exception message.</param>
    /// <returns>The close status, or null when the message carries none.</returns>
    public static int? ParseCloseStatus(string? message)
    {
        var match = CloseStatusPattern().Match(message ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["code"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
            ? code
            : null;
    }

    /// <summary>Parses the HTTP status from a refused upgrade message.</summary>
    /// <param name="message">The exception message.</param>
    /// <returns>The status, or null when the message carries none.</returns>
    public static int? ParseUpgradeStatus(string? message)
    {
        var match = UpgradeStatusPattern().Match(message ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["status"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var status)
            ? status
            : null;
    }

    /// <summary>Decides what to do after a link closed.</summary>
    /// <param name="message">The disconnected-transport message.</param>
    /// <returns>The decision.</returns>
    public static DispatchLinkDecision ClassifyClose(string? message)
    {
        var text = message ?? string.Empty;
        var match = CloseStatusPattern().Match(text);
        var code = ParseCloseStatus(text);
        if (code is null)
        {
            return Jitter(DispatchConnectionState.Reconnecting);
        }

        return code.Value switch
        {
            DispatchCloseCodes.DeviceRevoked => Stop(DispatchConnectionState.DeviceRevoked),
            DispatchCloseCodes.CredentialInvalid => Stop(DispatchConnectionState.CredentialExpired),
            DispatchCloseCodes.Replaced => new DispatchLinkDecision(DispatchLinkAction.RetryAfter, DispatchConnectionState.ReplacedByOtherInstance, SixtySeconds),
            DispatchCloseCodes.UnsupportedVersion => Stop(DispatchConnectionState.UpdateRequired),
            DispatchCloseCodes.AccountUnavailable => Stop(DispatchConnectionState.AccountUnavailable),
            DispatchCloseCodes.RateLimited => AdvertisedDelay(match),
            _ => Jitter(DispatchConnectionState.Reconnecting),
        };
    }

    /// <summary>Decides what to do after an upgrade was refused.</summary>
    /// <param name="message">The connect-failure message.</param>
    /// <param name="credentialExpiresAt">The expiry of the stored credential, or null when unreadable.</param>
    /// <param name="now">The current time on the injected clock.</param>
    /// <returns>The decision.</returns>
    public static DispatchLinkDecision ClassifyUpgrade(string? message, DateTimeOffset? credentialExpiresAt, DateTimeOffset now)
    {
        return ParseUpgradeStatus(message) switch
        {
            // A 401 with a credential that has not expired means the device was revoked; an expired credential needs a new pairing.
            401 when credentialExpiresAt is { } expiry && expiry > now => Stop(DispatchConnectionState.DeviceRevoked),
            401 => Stop(DispatchConnectionState.CredentialExpired),
            403 => Stop(DispatchConnectionState.AccountUnavailable),
            426 => Stop(DispatchConnectionState.UpdateRequired),
            404 => Jitter(DispatchConnectionState.DispatchDisabled),
            429 => new DispatchLinkDecision(DispatchLinkAction.RetryAfter, DispatchConnectionState.Reconnecting, SixtySeconds),
            _ => Jitter(DispatchConnectionState.Reconnecting),
        };
    }

    private static DispatchLinkDecision Stop(DispatchConnectionState state) => new(DispatchLinkAction.Stop, state, TimeSpan.Zero);

    private static DispatchLinkDecision Jitter(DispatchConnectionState state) => new(DispatchLinkAction.RetryWithJitter, state, TimeSpan.Zero);

    private static DispatchLinkDecision AdvertisedDelay(Match closeMatch)
    {
        // The rate-limit reason carries the delay in seconds. Without one the schedule falls back to jitter.
        if (closeMatch.Success && closeMatch.Groups["reason"].Success)
        {
            var digits = DelayPattern().Match(closeMatch.Groups["reason"].Value);
            if (digits.Success && int.TryParse(digits.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
            {
                return new DispatchLinkDecision(DispatchLinkAction.RetryAfter, DispatchConnectionState.Reconnecting, TimeSpan.FromSeconds(seconds));
            }
        }

        return Jitter(DispatchConnectionState.Reconnecting);
    }

    [GeneratedRegex(@"closed the connection \((?<code>\d+)(?: (?<reason>[^)]*))?\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CloseStatusPattern();

    [GeneratedRegex(@"\(HTTP (?<status>\d{3})\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UpgradeStatusPattern();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DelayPattern();
}
