using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Service;

/// <summary>Compare-and-swap, integrity, copy and quotas.</summary>
public sealed class ArtifactServiceTests
{
    [Fact]
    public async Task StaleBaseFailsAndSameHashReturnsTheExistingVersion()
    {
        var service = NewService(out _);
        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "one"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var updated = await service.UpdateAsync(created.Value!.ArtifactId, 1, "two"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        updated.Value!.Number.ShouldBe(2);

        var stale = await service.UpdateAsync(created.Value.ArtifactId, 1, "three"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        stale.IsSuccess.ShouldBeFalse();
        stale.Failure.ShouldNotBeNull().Code.ShouldBe("stale_base");
        stale.Failure.Latest.ShouldBe(2);

        var same = await service.UpdateAsync(created.Value.ArtifactId, 1, "two"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        same.Value!.Number.ShouldBe(2);
        var all = await service.ReadAllAsync(created.Value.ArtifactId, TestContext.Current.CancellationToken).ConfigureAwait(true);
        all.Value!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TamperedBytesOrParentLinkRefuseTheRead()
    {
        var service = NewService(out var store);
        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "one"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        store.TamperContent(created.Value!.ArtifactId, 1, "nope"u8.ToArray());

        var refused = await service.ReadAsync(created.Value.ArtifactId, 1, TestContext.Current.CancellationToken).ConfigureAwait(true);
        refused.IsSuccess.ShouldBeFalse();
        refused.Value.ShouldBeNull();
        refused.Failure.ShouldNotBeNull().Code.ShouldBe("integrity");

        var fresh = NewService(out var clean);
        var again = await fresh.CreateAsync(ArtifactKind.Markdown, "Notes", "one"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await fresh.UpdateAsync(again.Value!.ArtifactId, 1, "two"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        clean.TamperParentHash(again.Value.ArtifactId, 2, new string('a', 64));
        var broken = await fresh.ReadAsync(again.Value.ArtifactId, 2, TestContext.Current.CancellationToken).ConfigureAwait(true);
        broken.Failure.ShouldNotBeNull().Code.ShouldBe("integrity");
        broken.Value.ShouldBeNull();
    }

    [Fact]
    public async Task CopySetsDerivedFromAndStaleUserSaveIsRefused()
    {
        var service = NewService(out _);
        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "one"u8.ToArray(), "session", null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var copy = await service.CopyAsync(created.Value!.ArtifactId, TestContext.Current.CancellationToken).ConfigureAwait(true);
        copy.Value!.Number.ShouldBe(1);
        copy.Value.Title.ShouldBe("Notes (copy)");
        copy.Value.DerivedFrom.ShouldNotBeNull();
        copy.Value.DerivedFrom.ArtifactId.ShouldBe(created.Value.ArtifactId);
        copy.Value.DerivedFrom.Version.ShouldBe(1);
        copy.Value.DerivedFrom.ContentSha256.ShouldBe(created.Value.ContentSha256);
        copy.Value.SessionId.ShouldBe("session");

        var user = await service.SaveUserEditAsync(created.Value.ArtifactId, 1, "edited"u8.ToArray(), null, TestContext.Current.CancellationToken).ConfigureAwait(true);
        user.Value!.Author.ShouldBe(ArtifactAuthor.User);
        user.Value.Number.ShouldBe(2);

        var staleUser = await service.SaveUserEditAsync(created.Value.ArtifactId, 1, "other"u8.ToArray(), null, TestContext.Current.CancellationToken).ConfigureAwait(true);
        staleUser.Failure.ShouldNotBeNull().Code.ShouldBe("stale_base");
    }

    [Fact]
    public async Task OversizeAndVersionQuotaFailWithoutTruncation()
    {
        var options = new ArtifactOptions { MaxTextBytes = 4, MaxVersions = 2, MaxArtifacts = 1 };
        var service = new ArtifactService(new InMemoryArtifactStore(), TimeProvider.System, Options.Create(options));
        var over = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "12345"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        over.Failure.ShouldNotBeNull().Code.ShouldBe("too_large");

        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "12"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.Value!.Content.Length.ShouldBe(2);
        created.Value.Content.Span.SequenceEqual("12"u8).ShouldBeTrue();
        await service.UpdateAsync(created.Value.ArtifactId, 1, "34"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var quota = await service.UpdateAsync(created.Value.ArtifactId, 2, "56"u8.ToArray(), null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        quota.Failure.ShouldNotBeNull().Code.ShouldBe("quota_exceeded");
        var read = await service.ReadAsync(created.Value.ArtifactId, 2, TestContext.Current.CancellationToken).ConfigureAwait(true);
        read.Value!.Content.Span.SequenceEqual("34"u8).ShouldBeTrue();
    }

    private static ArtifactService NewService(out InMemoryArtifactStore store)
    {
        store = new InMemoryArtifactStore();
        return new ArtifactService(store, TimeProvider.System, Options.Create(new ArtifactOptions()));
    }
}
