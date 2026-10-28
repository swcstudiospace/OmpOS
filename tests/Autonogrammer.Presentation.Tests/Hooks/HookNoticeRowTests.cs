using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>The rows hooks add to the thread: a denial under its tool call, a collapsible context row, and the continuation of a stop hook.</summary>
public sealed class HookNoticeRowTests
{
    /// <summary>A denial names the hook and carries its message, is attached to the tool call, and is not collapsible.</summary>
    [Fact]
    public void ADenialNamesTheHookAndSitsUnderTheToolCall()
    {
        var row = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, "no-force-push", "call-7", "Force pushes are not allowed here."));

        row.Text.ShouldBe("denied by hook no-force-push: Force pushes are not allowed here.");
        row.IsAttachedToToolCall.ShouldBeTrue();
        row.ToolCallId.ShouldBe("call-7");
        row.IsCollapsible.ShouldBeFalse();
        row.OffersReview.ShouldBeFalse();
    }

    /// <summary>A denial without a message still names the hook.</summary>
    [Fact]
    public void ADenialWithoutAMessageStillNamesTheHook()
    {
        HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, "guard", null, string.Empty)).Text.ShouldBe("denied by hook guard");
        HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, string.Empty, null, "stop")).Text.ShouldBe("denied by a hook: stop");
    }

    /// <summary>Injected context is a collapsible row whose summary is fixed and whose detail is the context; credentials in it are redacted.</summary>
    [Fact]
    public void InjectedContextIsACollapsibleRow()
    {
        var row = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Context, "team-rules", null, "Use tabs. password=correct-horse-battery"));

        row.Text.ShouldBe(HooksCopy.ContextSummary);
        row.IsCollapsible.ShouldBeTrue();
        row.Detail.ShouldStartWith("Use tabs.");
        row.Detail.ShouldNotContain("correct-horse-battery");
        row.IsAttachedToToolCall.ShouldBeFalse();
    }

    /// <summary>A continuation says how many follow-ups the conversation has had, against the limit when there is one.</summary>
    [Fact]
    public void AContinuationCountsAgainstItsLimit()
    {
        HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.FollowUp, "stop", null, string.Empty, 2, 5)).Text.ShouldBe("Hook continued the conversation (2 of 5)");
        HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.FollowUp, "stop", null, string.Empty, 3, null)).Text.ShouldBe("Hook continued the conversation (3)");
    }

    /// <summary>A notice that a guarding hook has no approval offers the review action.</summary>
    [Fact]
    public void AMissingConsentOffersAReview()
    {
        var row = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.ConsentRequired, "guard", "c1", "ignored"));

        row.OffersReview.ShouldBeTrue();
        row.Text.ShouldBe("Hook guard guards this action and has no approval, so the action was blocked.");
    }
}
