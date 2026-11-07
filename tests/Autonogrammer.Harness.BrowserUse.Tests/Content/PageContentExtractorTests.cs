using Autonogrammer.Harness.BrowserUse.Content;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Content;

/// <summary>Visibility, invisible Unicode, the cap, taint and sensitive fields.</summary>
public sealed class PageContentExtractorTests
{
    [Fact]
    public void HiddenCss_Attributes_Comments_AndInvisibleUnicode_AreFiltered_AndCapped()
    {
        PageContentExtractor.IsHidden(0, "none", "hidden", 0.05, 2, true).ShouldBeTrue();
        var nodes = new List<PageExtractNode>
        {
            new("visible", true, PageTextKind.Text),
            new("hidden-css", false, PageTextKind.Text),
            new("alt-injection", true, PageTextKind.Attribute),
            new("comment", true, PageTextKind.Comment),
            new("meta", true, PageTextKind.Meta),
            new("noscript", true, PageTextKind.Noscript),
            new("a\u200Bb\u202Ac\U000E0001", true, PageTextKind.Text),
        };
        PageContentExtractor.Extract(nodes).ShouldBe("visible\nabc");
        PageContentExtractor.IsUntrusted.ShouldBeTrue();
        PageContentExtractor.Extract([new PageExtractNode(new string('x', 30000), true, PageTextKind.Text)]).Length.ShouldBe(20000);
    }

    [Fact]
    public void SensitiveFields_AreClassified_AndReadMasksThem()
    {
        SensitiveFieldClassifier.IsSensitive("password", null, null).ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", "cc-number", null).ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", "one-time-code", null).ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", null, "cardNumber").ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", null, "cvv").ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", null, "ssn").ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", null, "otp").ShouldBeTrue();
        SensitiveFieldClassifier.IsSensitive("text", null, "nickname").ShouldBeFalse();
        PageContentExtractor.Extract([new PageExtractNode("secret", true, PageTextKind.Text, Sensitive: true)]).ShouldBe(SensitiveFieldClassifier.Mask);
    }
}
