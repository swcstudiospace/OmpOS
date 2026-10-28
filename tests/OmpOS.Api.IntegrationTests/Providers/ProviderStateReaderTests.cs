using OmpOS.Api.Services.Providers;
using OmpOS.Contracts.Providers;
using OmpOS.Harness.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>How the service decides whether it has a key for a row: omp's credential, the environment variable of a block, or neither, and when it asks omp again.</summary>
public sealed class ProviderStateReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProviderStateReader Reader(FakeProviderBackend backend, FakeProviderEnvironment? environment = null, AdjustableClock? clock = null, int cacheSeconds = 15) =>
        new(backend, environment ?? new FakeProviderEnvironment(), Microsoft.Extensions.Options.Options.Create(new ProvidersOptions { StateCacheSeconds = cacheSeconds }), clock ?? new AdjustableClock());

    private static ProviderConfiguredState Of(ProviderStateReader reader, string id, ProviderBackendState state) => reader.ConfiguredOf(CatalogueRows.Find(id)!, state);

    [Fact]
    public void ANativeRowIsConfiguredWhenOmpHoldsItsCredentialOrItsVariableIsSet()
    {
        using var reader = Reader(new FakeProviderBackend(), new FakeProviderEnvironment("DEEPSEEK_API_KEY"));

        Of(reader, "deepseek", FakeProviderBackend.Installed()).ShouldBe(ProviderConfiguredState.Configured);
        Of(reader, "moonshot", FakeProviderBackend.Installed(credentialed: ["moonshot"])).ShouldBe(ProviderConfiguredState.Configured);
        Of(reader, "moonshot", FakeProviderBackend.Installed()).ShouldBe(ProviderConfiguredState.NotConfigured);
    }

    [Fact]
    public void ANativeRowIsUnknownWhenOmpCouldNotListItsModelsAndNothingProvesAKey()
    {
        using var reader = Reader(new FakeProviderBackend());

        Of(reader, "moonshot", FakeProviderBackend.Installed(complete: false)).ShouldBe(ProviderConfiguredState.Unknown);
        Of(reader, "moonshot", FakeProviderBackend.Installed(credentialed: ["moonshot"], complete: false)).ShouldBe(ProviderConfiguredState.Configured);
    }

    [Fact]
    public void ABlockRowIsConfiguredOnlyWhenItsBlockExistsAndItsVariableIsSet()
    {
        var blocks = new Dictionary<string, string?>(StringComparer.Ordinal) { ["zai-api"] = "OMPOS_PROVIDER_ZAI_API_KEY" };
        using var withKey = Reader(new FakeProviderBackend(), new FakeProviderEnvironment("OMPOS_PROVIDER_ZAI_API_KEY"));
        using var withoutKey = Reader(new FakeProviderBackend());

        Of(withKey, "zai-api", FakeProviderBackend.Installed(blocks: blocks)).ShouldBe(ProviderConfiguredState.Configured);
        Of(withoutKey, "zai-api", FakeProviderBackend.Installed(blocks: blocks)).ShouldBe(ProviderConfiguredState.NotConfigured);
        Of(withKey, "zai-api", FakeProviderBackend.Installed()).ShouldBe(ProviderConfiguredState.NotConfigured);
        Of(withKey, "zai-api", FakeProviderBackend.Installed(complete: false)).ShouldBe(ProviderConfiguredState.NotConfigured, "a block row is read from the file, not from the model listing");
    }

    [Fact]
    public void AKeylessRowNeedsNoKeyWhateverOmpReportsAndASignInRowIsConfiguredByItsCredential()
    {
        using var reader = Reader(new FakeProviderBackend());

        Of(reader, "ollama", ProviderBackendState.NoOmp).ShouldBe(ProviderConfiguredState.NotRequired);
        Of(reader, "openai-codex", FakeProviderBackend.Installed(credentialed: ["openai-codex"])).ShouldBe(ProviderConfiguredState.Configured);
        Of(reader, "openai-codex", FakeProviderBackend.Installed()).ShouldBe(ProviderConfiguredState.NotConfigured);
        Of(reader, "openai-codex", FakeProviderBackend.Installed(complete: false)).ShouldBe(ProviderConfiguredState.Unknown);
    }

    [Fact]
    public void WithoutOmpEveryKeyedRowIsUnknown()
    {
        using var reader = Reader(new FakeProviderBackend(), new FakeProviderEnvironment("DEEPSEEK_API_KEY"));

        CatalogueRows.All.Where(static row => row.Routes.Count == 0 || row.Routes.Any(static route => route.Auth != CatalogueAuthStyle.None))
            .ShouldAllBe(row => reader.ConfiguredOf(row, ProviderBackendState.NoOmp) == ProviderConfiguredState.Unknown);
    }

    [Fact]
    public void ACustomEndpointIsConfiguredWhenItsVariableIsSet()
    {
        using var reader = Reader(new FakeProviderBackend(), new FakeProviderEnvironment("OMPOS_PROVIDER_MINE_KEY"));
        var state = FakeProviderBackend.Installed();

        reader.CustomState("OMPOS_PROVIDER_MINE_KEY", state).ShouldBe(ProviderConfiguredState.Configured);
        reader.CustomState("OMPOS_PROVIDER_OTHER_KEY", state).ShouldBe(ProviderConfiguredState.NotConfigured);
        reader.CustomState(null, state).ShouldBe(ProviderConfiguredState.NotRequired);
        reader.CustomState("OMPOS_PROVIDER_MINE_KEY", ProviderBackendState.NoOmp).ShouldBe(ProviderConfiguredState.Unknown);
    }

    [Fact]
    public async Task TheStateIsReadOnceInsideTheCacheWindowAndAgainAfterItOrAfterAWrite()
    {
        var backend = new FakeProviderBackend();
        var clock = new AdjustableClock();
        using var reader = Reader(backend, clock: clock, cacheSeconds: 15);

        _ = await reader.ReadAsync(Token).ConfigureAwait(true);
        _ = await reader.ReadAsync(Token).ConfigureAwait(true);
        backend.Reads.ShouldBe(1);

        clock.Advance(TimeSpan.FromSeconds(16));
        _ = await reader.ReadAsync(Token).ConfigureAwait(true);
        backend.Reads.ShouldBe(2);

        reader.Invalidate();
        _ = await reader.ReadAsync(Token).ConfigureAwait(true);
        backend.Reads.ShouldBe(3);
    }

    [Fact]
    public async Task AZeroCacheWindowAsksOmpEveryTime()
    {
        var backend = new FakeProviderBackend();
        using var reader = Reader(backend, cacheSeconds: 0);

        _ = await reader.ReadAsync(Token).ConfigureAwait(true);
        _ = await reader.ReadAsync(Token).ConfigureAwait(true);

        backend.Reads.ShouldBe(2);
    }
}
