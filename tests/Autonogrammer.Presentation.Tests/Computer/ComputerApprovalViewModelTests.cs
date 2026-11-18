using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Computer;

/// <summary>The approval dialog's answer: a recorded choice is what every wait returns, a wait cannot hang, and the shown text drops characters that hide it.</summary>
public sealed class ComputerApprovalViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A wait that starts after the user chose Allow once returns Allow once, not the deny of a completed dialog.</summary>
    [Fact]
    public async Task AWaitAfterAnAllowOnceReturnsAllowOnce()
    {
        using var model = new ComputerApprovalViewModel(Facts(), new IdleClock());
        model.AllowOnce.Execute(null);

        (await model.WaitAsync(Token)).ShouldBe(ComputerApprovalChoice.AllowOnce);
        model.IsCompleted.ShouldBeTrue();
    }

    /// <summary>A second wait while the first is pending gets the same answer when it comes, so it cannot be left open.</summary>
    [Fact]
    public async Task ASecondWaitGetsTheSameChoiceInsteadOfHanging()
    {
        using var model = new ComputerApprovalViewModel(Facts(), new IdleClock());
        var first = model.WaitAsync(Token);
        var second = model.WaitAsync(Token);

        model.AllowForSession.Execute(null);

        (await first.WaitAsync(Token)).ShouldBe(ComputerApprovalChoice.AllowForSession);
        (await second.WaitAsync(Token)).ShouldBe(ComputerApprovalChoice.AllowForSession);
    }

    /// <summary>Cancelling the token of the wait is a deny, and the dialog is then complete.</summary>
    [Fact]
    public async Task CancellingTheWaitDenies()
    {
        using var cancellation = new CancellationTokenSource();
        using var model = new ComputerApprovalViewModel(Facts(), new IdleClock());
        var wait = model.WaitAsync(cancellation.Token);

        await cancellation.CancelAsync().ConfigureAwait(true);

        (await wait.WaitAsync(Token).ConfigureAwait(true)).ShouldBe(ComputerApprovalChoice.Deny);
        model.IsCompleted.ShouldBeTrue();
    }

    /// <summary>A wait whose token is already cancelled denies at once, and the dialog is then complete.</summary>
    [Fact]
    public async Task AWaitWithAnAlreadyCancelledTokenDenies()
    {
        using var cancellation = new CancellationTokenSource();
        using var model = new ComputerApprovalViewModel(Facts(), new IdleClock());
        await cancellation.CancelAsync().ConfigureAwait(true);

        (await model.WaitAsync(cancellation.Token).WaitAsync(Token).ConfigureAwait(true)).ShouldBe(ComputerApprovalChoice.Deny);
        model.IsCompleted.ShouldBeTrue();
    }

    /// <summary>Bidirectional and zero-width format characters are not shown in the application name.</summary>
    [Fact]
    public void FormatCharactersThatHideTextAreLeftOutOfTheShownName()
    {
        using var model = new ComputerApprovalViewModel(Facts() with { ApplicationName = "No\u202Ete\u200Es" }, new IdleClock());

        model.ApplicationName.ShouldBe("Notes");
    }

    private static ComputerApprovalFacts Facts() => new()
    {
        ApplicationName = "Notes",
        AppIdentity = "notes",
        SanitizedTitle = "Draft",
        HostActionWords = "click",
        HostSanitizedPreview = "hello",
        Origin = ComputerActionOrigin.Local,
    };

    private sealed class IdleClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();
    }

    private sealed class IdleTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
