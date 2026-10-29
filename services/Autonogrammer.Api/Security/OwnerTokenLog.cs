namespace Autonogrammer.Api.Security;

/// <summary>Log messages of the owner-token handler. None carries a token; the client address appears only as a short hash prefix.</summary>
internal static partial class OwnerTokenLog
{
    /// <summary>Logs a rejected owner-token attempt.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="addressHash">The first hex characters of the SHA-256 of the client address.</param>
    [LoggerMessage(EventId = 1, EventName = "OwnerTokenRejected", Level = LogLevel.Warning, Message = "Owner token rejected for client {AddressHash}")]
    public static partial void Rejected(ILogger logger, string addressHash);

    /// <summary>Logs an owner-token attempt answered 429 because the client address is over its failure budget.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="addressHash">The first hex characters of the SHA-256 of the client address.</param>
    [LoggerMessage(EventId = 2, EventName = "OwnerTokenThrottled", Level = LogLevel.Warning, Message = "Owner token attempts throttled for client {AddressHash}")]
    public static partial void Throttled(ILogger logger, string addressHash);
}
