namespace Autonogrammer.Presentation.Cloud;

/// <summary>Creates a cloud agent from a prompt: the one entry the composer's <c>&amp;</c> prefix and the in-cloud command use.</summary>
public interface ICloudHandoff
{
    /// <summary>
    /// Creates a cloud agent whose first task is the prompt. The caller has already shown <see cref="CloudCopy.CostNotice"/> and set
    /// <see cref="CloudHandoffRequest.CostConfirmed"/>; without it no request is made. This method does not throw an expected failure.
    /// </summary>
    /// <param name="request">What to hand off.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the hand-off ended.</returns>
    Task<CloudHandoffResult> StartAsync(CloudHandoffRequest request, CancellationToken cancellationToken);
}
