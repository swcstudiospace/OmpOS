using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>
/// Verifies the storage behaviour of the in-memory session store: replace, read, remove, listing, argument refusals
/// reported through the returned task, cancellation and concurrent writers.
/// </summary>
public sealed class InMemorySessionSecretStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StoredSecretIsReadBackByName()
    {
        InMemorySessionSecretStore store = new();
        var secret = new SecretString("stored-fixture-secret-91ac");

        await store.SetAsync("provider.openai", secret, Token).ConfigureAwait(true);

        SecretString? read = await store.GetAsync("provider.openai", Token).ConfigureAwait(true);
        read.ShouldNotBeNull();
        read.Reveal().ShouldBe("stored-fixture-secret-91ac");
    }

    [Fact]
    public async Task UnknownNameReadsAsNullAndRemovesAsFalse()
    {
        InMemorySessionSecretStore store = new();

        (await store.GetAsync("missing", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.RemoveAsync("missing", Token).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task SettingAnExistingNameReplacesTheSecret()
    {
        InMemorySessionSecretStore store = new();
        await store.SetAsync("key", new SecretString("first-value"), Token).ConfigureAwait(true);

        await store.SetAsync("key", new SecretString("second-value"), Token).ConfigureAwait(true);

        (await store.GetAsync("key", Token).ConfigureAwait(true))!.Reveal().ShouldBe("second-value");
        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["key"]);
    }

    [Fact]
    public async Task RemovedSecretIsGoneAndASecondRemovalReportsFalse()
    {
        InMemorySessionSecretStore store = new();
        await store.SetAsync("key", new SecretString("value"), Token).ConfigureAwait(true);

        (await store.RemoveAsync("key", Token).ConfigureAwait(true)).ShouldBeTrue();

        (await store.GetAsync("key", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.RemoveAsync("key", Token).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task NamesAreCaseSensitiveAndListedInOrdinalOrder()
    {
        InMemorySessionSecretStore store = new();
        await store.SetAsync("b", new SecretString("1"), Token).ConfigureAwait(true);
        await store.SetAsync("B", new SecretString("2"), Token).ConfigureAwait(true);
        await store.SetAsync("a", new SecretString("3"), Token).ConfigureAwait(true);

        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["B", "a", "b"]);
        (await store.GetAsync("B", Token).ConfigureAwait(true))!.Reveal().ShouldBe("2");
    }

    [Fact]
    public async Task EmptyStoreListsNoNames()
    {
        InMemorySessionSecretStore store = new();

        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ListedNamesAreASnapshotThatLaterWritesDoNotChange()
    {
        InMemorySessionSecretStore store = new();
        await store.SetAsync("one", new SecretString("1"), Token).ConfigureAwait(true);
        IReadOnlyList<string> snapshot = await store.ListNamesAsync(Token).ConfigureAwait(true);

        await store.SetAsync("two", new SecretString("2"), Token).ConfigureAwait(true);

        snapshot.ShouldBe(["one"]);
    }

    [Fact]
    public async Task SeparateStoresDoNotShareSecrets()
    {
        InMemorySessionSecretStore first = new();
        InMemorySessionSecretStore second = new();

        await first.SetAsync("key", new SecretString("value"), Token).ConfigureAwait(true);

        (await second.GetAsync("key", Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public async Task BlankNameIsRefusedByEveryMemberThroughTheReturnedTask(string name)
    {
        InMemorySessionSecretStore store = new();

        await Should.ThrowAsync<ArgumentException>(store.SetAsync(name, new SecretString("value"), Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(store.GetAsync(name, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(store.RemoveAsync(name, Token)).ConfigureAwait(true);
        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NullNameAndNullSecretAreReportedThroughTheReturnedTask()
    {
        InMemorySessionSecretStore store = new();

        ArgumentNullException nameFault = await Should.ThrowAsync<ArgumentNullException>(
            store.SetAsync(null!, new SecretString("value"), Token)).ConfigureAwait(true);
        ArgumentNullException secretFault = await Should.ThrowAsync<ArgumentNullException>(
            store.SetAsync("key", null!, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentNullException>(store.GetAsync(null!, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentNullException>(store.RemoveAsync(null!, Token)).ConfigureAwait(true);

        nameFault.ParamName.ShouldBe("name");
        secretFault.ParamName.ShouldBe("secret");
        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelledTokenIsReportedThroughTheReturnedTaskAndChangesNothing()
    {
        InMemorySessionSecretStore store = new();
        await store.SetAsync("kept", new SecretString("value"), Token).ConfigureAwait(true);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(
            store.SetAsync("new", new SecretString("value"), cancelled.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(store.GetAsync("kept", cancelled.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(store.RemoveAsync("kept", cancelled.Token)).ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(store.ListNamesAsync(cancelled.Token)).ConfigureAwait(true);

        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["kept"]);
    }

    [Fact]
    public async Task ConcurrentWritersToDistinctNamesAllLand()
    {
        InMemorySessionSecretStore store = new();
        const int Writers = 64;

        await Task.WhenAll(Enumerable.Range(0, Writers).Select(index =>
            Task.Run(() => store.SetAsync($"key-{index:D3}", new SecretString($"value-{index}"), Token), Token))).ConfigureAwait(true);

        IReadOnlyList<string> names = await store.ListNamesAsync(Token).ConfigureAwait(true);
        names.Count.ShouldBe(Writers);
        (await store.GetAsync("key-017", Token).ConfigureAwait(true))!.Reveal().ShouldBe("value-17");
    }
}
