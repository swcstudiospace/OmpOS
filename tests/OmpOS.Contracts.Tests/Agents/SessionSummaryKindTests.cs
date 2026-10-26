using OmpOS.Contracts.Agents;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Agents;

/// <summary>
/// Verifies that a session summary is a chat unless it says otherwise, so every producer that predates the session kind keeps
/// describing a chat session.
/// </summary>
public sealed class SessionSummaryKindTests
{
    private static readonly DateTimeOffset Moment = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static SessionSummary Summary() => new()
    {
        Id = "s-1",
        Title = "Fix login",
        Lifecycle = SessionLifecycle.Running,
        CreatedAt = Moment,
        LastActivity = Moment,
    };

    [Fact]
    public void ASummaryThatDoesNotSetAKindIsAChatSession()
    {
        Summary().Kind.ShouldBe(SessionKind.Chat);
        default(SessionKind).ShouldBe(SessionKind.Chat);
    }

    [Fact]
    public void TheKindIsCarriedByACopyAndDoesNotChangeTheOtherMembers()
    {
        var terminal = Summary() with { Kind = SessionKind.Terminal };

        terminal.Kind.ShouldBe(SessionKind.Terminal);
        terminal.ShouldBe(Summary() with { Kind = SessionKind.Terminal });
        terminal.ShouldNotBe(Summary());
        terminal.Id.ShouldBe("s-1");
        terminal.Title.ShouldBe("Fix login");
    }
}
