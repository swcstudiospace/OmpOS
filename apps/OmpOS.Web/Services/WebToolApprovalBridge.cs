using OmpOS.Presentation.Approvals;

namespace OmpOS.Web.Services;

/// <summary>The <see cref="IToolApprovalBridge"/> of the web head: it sends the decision to <c>POST /sessions/{id}/tool-approvals/{requestId}</c> and maps the answer.</summary>
internal sealed class WebToolApprovalBridge : IToolApprovalBridge
{
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="WebToolApprovalBridge"/> class.</summary>
    /// <param name="api">The API client.</param>
    internal WebToolApprovalBridge(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Occurs after the API answered a decision, with the session it belongs to, so the owner of the session refreshes its pending list.</summary>
    internal event EventHandler<ApprovalDecidedEventArgs>? Decided;

    /// <inheritdoc />
    public async Task<ToolApprovalOutcome> DecideAsync(
        string sessionId,
        string requestId,
        ToolApprovalDecision decision,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        try
        {
            await _api.DecideToolApprovalAsync(sessionId, requestId, decision == ToolApprovalDecision.Allow, cancellationToken).ConfigureAwait(true);
            Decided?.Invoke(this, new ApprovalDecidedEventArgs(sessionId));
            return ToolApprovalOutcome.Accepted;
        }
        catch (ApiProblemException problem)
        {
            Decided?.Invoke(this, new ApprovalDecidedEventArgs(sessionId));
            return problem.Kind switch
            {
                ApiProblemKind.NotFound => ToolApprovalOutcome.NotFound,
                ApiProblemKind.Conflict => ToolApprovalOutcome.AlreadyDecided,
                ApiProblemKind.Forbidden => ToolApprovalOutcome.Refused,
                _ => ToolApprovalOutcome.Failed,
            };
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return ToolApprovalOutcome.Failed;
        }
    }
}
