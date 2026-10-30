using Autonogrammer.Contracts.Artifacts;

using Autonogrammer.Cowork.Artifacts.Model;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Stores.Tests.Support;

/// <summary>The store behaviours every implementation must keep: create, compare-and-swap, idempotent hash, stale base, copy and delete.</summary>
public abstract class ArtifactStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates a store scoped to one account.</summary>
    /// <returns>The store.</returns>
    protected abstract IArtifactStore CreateStore();

    /// <summary>Create, update, same-hash retry, stale base, copy and delete all hold.</summary>
    /// <returns>A task that completes when the contract has run.</returns>
    [Fact]
    public async Task Create_update_idempotent_stale_copy_and_delete()
    {
        var service = new ArtifactService(CreateStore(), TimeProvider.System, Options.Create(new ArtifactOptions()));
        var created = await service.CreateAsync(ArtifactKind.Markdown, "Notes", "one"u8.ToArray(), "session", null, null, null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        created.IsSuccess.ShouldBeTrue();
        var id = created.Value!.ArtifactId;

        var updated = await service.UpdateAsync(id, 1, "two"u8.ToArray(), null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        updated.IsSuccess.ShouldBeTrue();
        updated.Value!.Number.ShouldBe(2);

        var same = await service.UpdateAsync(id, 2, "two"u8.ToArray(), null, ArtifactAuthor.Agent, Token).ConfigureAwait(true);
        same.IsSuccess.ShouldBeTrue();
        same.Value!.Number.ShouldBe(2);

        var stale = await service.UpdateAsync(id, 1, "three"u8.ToArray(), null, ArtifactAuthor.User, Token).ConfigureAwait(true);
        stale.IsSuccess.ShouldBeFalse();
        stale.Failure!.Code.ShouldBe("stale_base");
        stale.Failure.Latest.ShouldBe(2);

        var copy = await service.CopyAsync(id, Token).ConfigureAwait(true);
        copy.IsSuccess.ShouldBeTrue();
        string.Equals(copy.Value!.ArtifactId, id, StringComparison.Ordinal).ShouldBeFalse();
        copy.Value.DerivedFrom.ShouldNotBeNull();
        copy.Value.DerivedFrom!.ContentSha256.ShouldBe(updated.Value.ContentSha256);

        await service.DeleteAsync(id, Token).ConfigureAwait(true);
        var gone = await service.ReadAsync(id, 2, Token).ConfigureAwait(true);
        gone.IsSuccess.ShouldBeFalse();
        gone.Failure!.Code.ShouldBe("not_found");
        var kept = await service.ReadAsync(copy.Value.ArtifactId, 1, Token).ConfigureAwait(true);
        kept.IsSuccess.ShouldBeTrue();
    }
}
