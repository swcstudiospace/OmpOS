using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.UI.Components.Dispatch;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Dispatch;

/// <summary>Standalone non-modal approval actions.</summary>
public sealed class DispatchApprovalFocusTests : UiTestContext
{
    [Theory]
    [InlineData(0, "approve")]
    [InlineData(1, "deny")]
    [InlineData(2, "deny and pause")]
    public void EachApprovalActionInvokesOnlyItsOwnCallback(int index, string expected)
    {
        var invoked = new List<string>();
        var approval = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");
        var cut = Render<DispatchApprovalPanel>(parameters => parameters
            .Add(p => p.Model, approval)
            .Add(p => p.OnApprove, EventCallback.Factory.Create(this, () => invoked.Add("approve")))
            .Add(p => p.OnDeny, EventCallback.Factory.Create(this, () => invoked.Add("deny")))
            .Add(p => p.OnDenyAndPause, EventCallback.Factory.Create(this, () => invoked.Add("deny and pause"))));

        cut.FindAll("button")[index].Click();

        invoked.ShouldBe([expected]);
        JSInterop.Invocations.Count.ShouldBe(0);
    }
}
