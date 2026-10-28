using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.Cloud;

/// <summary>Turns an exception from provisioning or waking into the error stored on the agent. The message is fixed text chosen by the failure kind: a provider's own message is never copied, so a secret echoed by a provider cannot reach a response.</summary>
internal static class CloudFailureMap
{
    /// <summary>Maps an exception to the agent error.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The error to store.</returns>
    public static CloudAgentError From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            CloudProvisioningException known => new CloudAgentError { Code = known.Code, Message = known.Message },
            CloudEnvironmentException environment => FromEnvironment(environment.Failure),
            HttpRequestException => Error("provider_unreachable", "The worker provider could not be reached."),
            _ => Error("provisioning_failed", "The worker could not be provisioned."),
        };
    }

    private static CloudAgentError FromEnvironment(CloudEnvironmentFailure failure) => failure switch
    {
        CloudEnvironmentFailure.Unauthorized => Error("provider_unauthorized", "The worker provider refused the server's credential."),
        CloudEnvironmentFailure.RateLimited => Error("provider_rate_limited", "The worker provider is rate limiting the server. Try again later."),
        CloudEnvironmentFailure.Transport => Error("provider_unreachable", "The worker provider could not be reached."),
        CloudEnvironmentFailure.NotFound => Error("worker_missing", "The worker no longer exists at the provider."),
        CloudEnvironmentFailure.WakeTimeout => Error("wake_timeout", "The worker did not wake in time."),
        CloudEnvironmentFailure.CapacityExhausted => Error("capacity_exhausted", "The worker provider has no capacity for another worker."),
        CloudEnvironmentFailure.Unsupported => Error("provider_unsupported", "The worker provider does not support this operation."),
        _ => Error("provider_error", "The worker provider reported an error."),
    };

    private static CloudAgentError Error(string code, string message) => new() { Code = code, Message = message };
}
