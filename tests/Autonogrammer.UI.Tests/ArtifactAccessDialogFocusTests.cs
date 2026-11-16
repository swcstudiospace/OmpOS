using Autonogrammer.Presentation.Cowork.Artifacts;
using Autonogrammer.UI.Components.Cowork.Artifacts;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Focus lifecycle and decision handling for the standalone artifact access dialog.</summary>
public sealed class ArtifactAccessDialogFocusTests : UiTestContext
{
    private const string DialogModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-dialog.js";
    private const int TrapToken = 17;

    [Fact]
    public async Task OpeningCapturesTheOpenerBeforeFocusingDenyAndDisposalReleasesTheTrap()
    {
        var module = SetupDialogModule();
        var cut = Render<ArtifactAccessDialog>();

        await cut.WaitForAssertionAsync(() =>
        {
            module.VerifyInvoke("trapFocus").Arguments[0].ShouldBeElementReferenceTo(cut.Find("[role='dialog']"));
            JSInterop.FocusCalls().ShouldHaveSingleItem().Arguments[0].ShouldBeElementReferenceTo(cut.Find("button"));
        }).ConfigureAwait(true);
        cut.Find("button").TextContent.Trim().ShouldBe("Deny");
        cut.Find("button").HasAttribute("autofocus").ShouldBeFalse("the trap must capture the opener before focus moves");

        await DisposeComponentsAsync().ConfigureAwait(true);
        module.VerifyInvoke("releaseFocus").Arguments.ShouldBe([TrapToken]);
    }

    [Theory]
    [InlineData(0, ArtifactAccessChoice.Deny)]
    [InlineData(1, ArtifactAccessChoice.AllowVersion)]
    [InlineData(2, ArtifactAccessChoice.AllowSession)]
    public void EachChoiceNotifiesTheHostWithoutPrematurelyReleasingFocus(int index, ArtifactAccessChoice expected)
    {
        var module = SetupDialogModule();
        var choices = new List<ArtifactAccessChoice>();
        var cut = Render<ArtifactAccessDialog>(parameters => parameters
            .Add(p => p.OnChoose, EventCallback.Factory.Create<ArtifactAccessChoice>(this, choices.Add)));

        cut.FindAll("button")[index].Click();

        choices.ShouldBe([expected]);
        module.VerifyNotInvoke("releaseFocus");
    }

    [Fact]
    public void EscapeDeniesAndOtherKeysDoNotChoose()
    {
        SetupDialogModule();
        var choices = new List<ArtifactAccessChoice>();
        var cut = Render<ArtifactAccessDialog>(parameters => parameters
            .Add(p => p.OnChoose, EventCallback.Factory.Create<ArtifactAccessChoice>(this, choices.Add)));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Tab" });
        choices.ShouldBeEmpty();
        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        choices.ShouldBe([ArtifactAccessChoice.Deny]);
    }

    private BunitJSModuleInterop SetupDialogModule()
    {
        var module = JSInterop.SetupModule(DialogModule);
        module.Setup<int>("trapFocus", _ => true).SetResult(TrapToken);
        return module;
    }
}
