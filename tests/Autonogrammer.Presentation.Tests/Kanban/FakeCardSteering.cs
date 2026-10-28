using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>A steering service the test scripts: it records every call and answers from <see cref="Answer"/>, or waits on <see cref="Gate"/> first.</summary>
internal sealed class FakeCardSteering : ICardSteering
{
    /// <summary>Gets the calls received, in order.</summary>
    internal List<SteerCall> Calls { get; } = [];

    /// <summary>Gets the tokens received, in order.</summary>
    internal List<CancellationToken> Tokens { get; } = [];

    /// <summary>Gets or sets what the service answers. The default delivers.</summary>
    internal Func<SteerCall, CardSteerResult> Answer { get; set; } = static _ => CardSteerResult.Delivered();

    /// <summary>Gets or sets a task the service waits for before it answers, or null to answer at once.</summary>
    internal Task? Gate { get; set; }

    /// <inheritdoc />
    public async Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken)
    {
        var call = new SteerCall(sessionId, text, mode);
        Calls.Add(call);
        Tokens.Add(cancellationToken);
        if (Gate is { } gate)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return Answer(call);
    }

    /// <summary>One call the service received.</summary>
    /// <param name="SessionId">The session.</param>
    /// <param name="Text">The text.</param>
    /// <param name="Mode">The mode.</param>
    internal sealed record SteerCall(string SessionId, string Text, CardSteerMode Mode);
}
