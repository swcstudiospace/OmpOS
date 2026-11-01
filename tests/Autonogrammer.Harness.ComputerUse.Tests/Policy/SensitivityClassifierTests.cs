using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Sensitivity is computed from host-observed titles and action class only.</summary>
public sealed class SensitivityClassifierTests
{
    [Theory]
    [InlineData("Sign in to mail")]
    [InlineData("Log in")]
    [InlineData("Password")]
    [InlineData("Passcode")]
    [InlineData("One-time code")]
    [InlineData("API token")]
    [InlineData("client secret")]
    [InlineData("Unlock vault")]
    public void CredentialTitlesAlwaysAsk(string title)
    {
        Classify(title, ComputerActionClass.Type, autoApproveTyping: true, first: false, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.CredentialTitle);
    }

    [Fact]
    public void FileDeletionPurchaseRaisedClipboardFirstActionAndDispatchAsk()
    {
        Classify("Delete file", ComputerActionClass.Click, false, false, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.FileDeletion);
        Classify("Checkout", ComputerActionClass.Click, false, false, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.Purchase);
        Classify("Inbox", ComputerActionClass.Click, true, false, ComputerActionOrigin.Local, true, false)
            .ShouldBe(SensitivityReason.RaisedCategory);
        Classify("Notes", ComputerActionClass.Clipboard, true, false, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.Clipboard);
        Classify("Notes", ComputerActionClass.Click, false, true, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.FirstAction);
        Classify("Notes", ComputerActionClass.Click, true, false, ComputerActionOrigin.Dispatch, false, false)
            .ShouldBe(SensitivityReason.DispatchOrigin);
    }

    [Fact]
    public void TypingWithAutoApproveInANonCredentialAppDoesNotAsk()
    {
        Classify("Notes", ComputerActionClass.Type, autoApproveTyping: true, first: false, ComputerActionOrigin.Local, false, false)
            .ShouldBe(SensitivityReason.None);
    }

    private static SensitivityReason Classify(
        string title,
        ComputerActionClass actionClass,
        bool autoApproveTyping,
        bool first,
        ComputerActionOrigin origin,
        bool raised,
        bool fileDeletion) =>
        SensitivityClassifier.Classify(title, raised, actionClass, autoApproveTyping, first, origin, fileDeletion);
}
