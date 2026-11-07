namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The local approval dialog for a dispatched Computer Use task. Implementations deny when no choice is made.</summary>
public interface IDispatchApprovalPrompt
{
    /// <summary>Shows the request and waits for a local choice.</summary>
    /// <param name="request">The facts to show.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The local choice.</returns>
    ValueTask<DispatchApprovalChoice> RequestAsync(DispatchApprovalRequest request, CancellationToken cancellationToken);
}
