namespace Autonogrammer.Api.Dispatch.Devices;

/// <summary>Handles an authenticated connect request. Phase 74-05 replaces the default with the hub.</summary>
internal interface IDispatchConnectHandler
{
    /// <summary>Handles the connect request after the device credential and the owner check have passed.</summary>
    /// <param name="context">The request.</param>
    /// <param name="deviceId">The device identifier from the route.</param>
    /// <param name="accountId">The account from the credential <c>own</c> claim.</param>
    /// <param name="cancellationToken">Cancels the handler.</param>
    /// <returns>The HTTP result. The default answers 503 <c>hub_not_ready</c>.</returns>
    Task<IResult> HandleAsync(HttpContext context, string deviceId, string accountId, CancellationToken cancellationToken);
}
