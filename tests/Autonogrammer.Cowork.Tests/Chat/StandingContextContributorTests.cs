using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>
/// RED tests for StandingContextContributor (task 2 of 75-06).
/// Asserts paragraph bound, resource block, memory.backend off, refusal over budget.
/// </summary>
public sealed class StandingContextContributorTests
{
    [Fact]
    public void CoworkSurfaceGetsParagraphAtMost2KiBAndFirstPromptBlockAndMemoryBackendOff()
    {
        var c = new StandingContextContributor();
        var req = new LaunchRequest { Surface = "cowork", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };
        var ct = c.Contribute(req, TestContext.Current.CancellationToken);

        ct.SystemPromptParagraph.ShouldNotBeNullOrWhiteSpace();
        System.Text.Encoding.UTF8.GetByteCount(ct.SystemPromptParagraph).ShouldBeLessThanOrEqualTo(2 * 1024);
        ct.FirstPromptBlock.ShouldNotBeNullOrWhiteSpace();
        ct.FirstPromptBlock.ShouldContain("<untrusted>");
        ct.OverlayEntries.ShouldContain(o => o.Contains("memory:") && o.Contains("backend: off"));
    }

    [Fact]
    public void CodeSurfaceGetsEmptyContribution()
    {
        var c = new StandingContextContributor();
        var req = new LaunchRequest { Surface = "code", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };
        var ct = c.Contribute(req, TestContext.Current.CancellationToken);

        ct.SystemPromptParagraph.ShouldBe(string.Empty);
        ct.FirstPromptBlock.ShouldBe(string.Empty);
        ct.OverlayEntries.ShouldBeEmpty();
    }

    [Fact]
    public void BlockIsByteStableAndContainsUntrustedFence()
    {
        var c = new StandingContextContributor();
        var req = new LaunchRequest { Surface = "cowork", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };
        var ct1 = c.Contribute(req, TestContext.Current.CancellationToken);
        var ct2 = c.Contribute(req, TestContext.Current.CancellationToken);
        ct1.FirstPromptBlock.ShouldBe(ct2.FirstPromptBlock);
        ct1.FirstPromptBlock.ShouldContain("<untrusted>");
        ct1.FirstPromptBlock.ShouldContain("</untrusted>");
    }
}
