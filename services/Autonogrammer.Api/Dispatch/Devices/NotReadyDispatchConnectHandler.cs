using Autonogrammer.Api.Services;

namespace Autonogrammer.Api.Dispatch.Devices;

/// <summary>The connect handler registered until the hub exists. Answers 503 <c>hub_not_ready</c>.</summary>
internal sealed class NotReadyDispatchConnectHandler : IDispatchConnectHandler
{
    /// <inheritdoc />
    public Task<IResult> HandleAsync(HttpContext context, string deviceId, string accountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        cancellationToken.ThrowIfCancellationRequested();
        IResult problem = WebProblems.Of(StatusCodes.Status503ServiceUnavailable, "hub_not_ready", "Hub not ready.", "The dispatch hub is not registered.");
        return Task.FromResult(problem);
    }
}
