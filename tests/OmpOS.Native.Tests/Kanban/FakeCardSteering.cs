using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>A steering service the view tests script: it records every call and answers from <see cref="Answer"/>.</summary>
internal sealed class FakeCardSteering : ICardSteering
{
    /// <summary>Gets the calls received, in order.</summary>
    internal List<SteerCall> Calls { get; } = [];

    /// <summary>Gets or sets what the service answers. The default delivers.</summary>
    internal Func<SteerCall, CardSteerResult> Answer { get; set; } = static _ => CardSteerResult.Delivered();

    /// <inheritdoc />
    public Task<CardSteerResult> SteerAsync(string sessionId, string text, CardSteerMode mode, CancellationToken cancellationToken)
    {
        var call = new SteerCall(sessionId, text, mode);
        Calls.Add(call);
        return Task.FromResult(Answer(call));
    }

    /// <summary>One call the service received.</summary>
    /// <param name="SessionId">The session.</param>
    /// <param name="Text">The text.</param>
    /// <param name="Mode">The mode.</param>
    internal sealed record SteerCall(string SessionId, string Text, CardSteerMode Mode);
}
