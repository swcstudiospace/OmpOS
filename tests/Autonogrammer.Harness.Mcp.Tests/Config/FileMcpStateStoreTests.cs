using Autonogrammer.Harness.Mcp.Catalog;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>The Autonogrammer-owned state file: enabled flags and consent survive a restart, and a damaged file never grants consent.</summary>
public sealed class FileMcpStateStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string StatePath => Path.Combine(_directory.Path, "nested", "mcp-state.json");

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task AMissingFileIsEmptyState()
    {
        var state = await new FileMcpStateStore(StatePath).LoadAsync(Token).ConfigureAwait(true);

        state.EnabledOverrides.ShouldBeEmpty();
        state.ConsentedIdentities.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChoicesRoundTripThroughTheFileAndTheDirectoryIsCreated()
    {
        var store = new FileMcpStateStore(StatePath);
        var saved = McpState.Empty.WithEnabled("docs", false).WithEnabled("search", true).WithConsent("b".PadRight(64, 'b')).WithConsent("a".PadRight(64, 'a'));

        await store.SaveAsync(saved, Token).ConfigureAwait(true);
        var loaded = await new FileMcpStateStore(StatePath).LoadAsync(Token).ConfigureAwait(true);

        loaded.EnabledOverride("docs").ShouldBe(false);
        loaded.EnabledOverride("search").ShouldBe(true);
        loaded.EnabledOverride("other").ShouldBeNull();
        loaded.IsConsented("a".PadRight(64, 'a')).ShouldBeTrue();
        loaded.IsConsented("c".PadRight(64, 'c')).ShouldBeFalse();
        Directory.GetFiles(Path.GetDirectoryName(StatePath)!).Select(file => Path.GetFileName(file)).ShouldBe(["mcp-state.json"]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("""{ "version": 99, "enabled": {}, "consented": ["x"] }""")]
    [InlineData("""{ "version": 1, "enabled": {}, "consented": [], "extra": true }""")]
    public async Task ADamagedOrForeignFileIsEmptyStateAndIsMovedAside(string content)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        await File.WriteAllTextAsync(StatePath, content, Token).ConfigureAwait(true);

        var state = await new FileMcpStateStore(StatePath).LoadAsync(Token).ConfigureAwait(true);

        state.ConsentedIdentities.ShouldBeEmpty();
        File.Exists(StatePath).ShouldBeFalse();
        File.Exists(StatePath + ".damaged").ShouldBeTrue();
    }

    [Fact]
    public void StateValuesAreImmutable()
    {
        var empty = McpState.Empty;

        var changed = empty.WithEnabled("a", true).WithConsent("id");
        var withdrawn = changed.WithoutConsent("id");

        empty.EnabledOverrides.ShouldBeEmpty();
        empty.ConsentedIdentities.ShouldBeEmpty();
        changed.IsConsented("id").ShouldBeTrue();
        withdrawn.IsConsented("id").ShouldBeFalse();
        withdrawn.EnabledOverride("a").ShouldBe(true);
    }
}
