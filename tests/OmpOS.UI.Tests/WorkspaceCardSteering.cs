using OmpOS.Presentation.Kanban;

namespace OmpOS.UI.Tests;

/// <summary>A steering service the test scripts: it records every call and answers with the result the test chose.</summary>
internal sealed class WorkspaceCardSteering : ICardSteering
{
    /// <summary>Gets the calls received, as session, text and mode.</summary>
    internal List<(string SessionId, string Text, CardSteerMode Mode)> Calls { get; } = [];

    /// <summary>Gets or sets the answer to the next call.</summary>
    internal CardSteerResult Result { get; set; } = CardSteerResult.Delivered();

    /// <inheritdoc />
    public Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken)
    {
        Calls.Add((sessionId, text, mode));
        return Task.FromResult(Result);
    }
}
