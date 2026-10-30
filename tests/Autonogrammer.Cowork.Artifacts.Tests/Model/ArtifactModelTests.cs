using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Model;

/// <summary>Title rules and option-key naming.</summary>
public sealed class ArtifactModelTests
{
    [Fact]
    public async Task TitleOf120SucceedsAndForbiddenTitlesFail()
    {
        var service = NewService();
        var ok = await service.CreateAsync(ArtifactKind.Markdown, new string('a', 120), "x"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        ok.IsSuccess.ShouldBeTrue();

        var longTitle = await service.CreateAsync(ArtifactKind.Markdown, new string('a', 121), "x"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        longTitle.Failure.ShouldNotBeNull().Code.ShouldBe("title_invalid");
        var lone = await service.CreateAsync(ArtifactKind.Markdown, "bad\uD800", "x"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var bidi = await service.CreateAsync(ArtifactKind.Markdown, "left" + '\u202E' + "right", "x"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        bidi.Failure.ShouldNotBeNull().Code.ShouldBe("title_invalid");

        lone.Failure.ShouldNotBeNull().Code.ShouldBe("title_invalid");
    }

    [Fact]
    public void ValidatorNamesTheKeyAndNeverTheValue()
    {
        var options = new ArtifactOptions { MaxTextBytes = 0, Surface = "secret-surface-value" };
        var result = new ArtifactOptionsValidator().Validate(null, options);

        result.Failed.ShouldBeTrue();
        var joined = string.Join(" ", result.Failures);
        joined.ShouldContain("Cowork:Artifacts:MaxTextBytes");
        joined.ShouldNotContain("secret-surface-value");
        joined.ShouldNotContain('0');
    }

    private static ArtifactService NewService() =>
        new(new InMemoryArtifactStore(), TimeProvider.System, Options.Create(new ArtifactOptions()));
}
