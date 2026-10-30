using Autonogrammer.Contracts.Controls;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Web.Services;

/// <summary>The <see cref="ICardSteering"/> of the web head: a steer from a Kanban card goes to <c>POST /sessions/{id}/steer</c>. A refusal is a result, never an exception.</summary>
internal sealed class WebCardSteering : ICardSteering
{
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="WebCardSteering"/> class.</summary>
    /// <param name="api">The API client.</param>
    internal WebCardSteering(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public async Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.SteerAsync(sessionId, text, mode == CardSteerMode.FollowUp ? SteerMode.Queue : SteerMode.SendNow, cancellationToken).ConfigureAwait(true);
            return response.Delivery switch
            {
                SteerDelivery.Delivered => CardSteerResult.Delivered(),
                SteerDelivery.Queued => CardSteerResult.Queued(),
                SteerDelivery.Held => CardSteerResult.Queued("Held until the turn ends"),
                _ => CardSteerResult.Failed(string.IsNullOrWhiteSpace(response.Reason) ? "The agent did not take the message." : response.Reason),
            };
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return CardSteerResult.Failed(WebFailureText.Describe(fault));
        }
    }
}
