namespace Autonogrammer.Presentation.Kanban;

/// <summary>The steering service a card sends its message through. Delivery and its truthful report are the service's job.</summary>
public interface ICardSteering
{
    /// <summary>Sends a message to a session.</summary>
    /// <param name="sessionId">The session the card stands for.</param>
    /// <param name="text">The message, as the user typed it.</param>
    /// <param name="mode">Whether to deliver now or queue for the end of the turn.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>What happened. A session that ended, disconnected or never started is a <see cref="CardSteerOutcome.Failed"/> result.</returns>
    Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken);
}
