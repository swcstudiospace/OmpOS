using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests.Live;

/// <summary>
/// Live proof against the real <c>omp</c> that the library changes what omp uses: a managed provider written to <c>models.yml</c>
/// appears in <c>omp models --json</c>, a change omp rejects is rolled back, a key imported through <c>omp auth import -</c> is
/// resolved by <c>omp token</c> and kept with owner-only permissions, and setting the default model keeps the other roles. Every
/// test runs in a temporary home with the fake key <c>sk-fake-0000-test</c> and sends no model prompt. Excluded from the gate by its
/// trait and skipped unless <c>AUTONOGRAMMER_LIVE_OMP=1</c>; run it with
/// <c>AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.OmpConfig.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
/// <remarks>
/// The first <c>omp models --json</c> of a brand-new agent directory may refresh omp's catalog over the network. When omp cannot
/// answer within the time limit (an offline cold cache), the listing assertions are skipped and the reason is written to the test
/// output; the file-level assertions that do not need the listing still run where they can.
/// </remarks>
[Trait("Category", "Live")]
[UnsupportedOSPlatform("windows")]
public sealed class LiveOmpConfigTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_OMP";

    [Fact]
    public async Task AManagedProviderAppearsInTheRealModelsListingAndLeavesWithItsBlock()
    {
        using var sandbox = ArrangeSandbox();
        var configuration = new OmpModelConfiguration(sandbox.Options());
        var cancellationToken = TestContext.Current.CancellationToken;
        var provider = LiveProvider("autonogrammer-live");

        var written = await configuration.ModelsFile.UpsertAsync(provider, cancellationToken).ConfigureAwait(true);
        SkipWhenOmpCouldNotAnswer(written);

        written.Status.ShouldBe(OmpModelsWriteStatus.Written, written.Message);
        var read = await configuration.ModelsFile.ReadManagedAsync(cancellationToken).ConfigureAwait(true);
        read.Problems.ShouldBeEmpty();
        read.Providers.Single().Id.ShouldBe("autonogrammer-live");
        read.Providers.Single().Models.ShouldBe(provider.Models);

        var listing = await configuration.Catalog.ListAsync(cancellationToken).ConfigureAwait(true);
        SkipWhenListingIsIncomplete(listing);
        listing.Models.ShouldContain(model => string.Equals(model.Selector, "autonogrammer-live/live-small", StringComparison.Ordinal));
        var row = listing.Models.Single(model => string.Equals(model.Selector, "autonogrammer-live/live-small", StringComparison.Ordinal));
        row.DisplayName.ShouldBe("Live Small");
        row.ContextWindow.ShouldBe(8192);

        var removed = await configuration.ModelsFile.RemoveAsync("autonogrammer-live", cancellationToken).ConfigureAwait(true);
        removed.Status.ShouldBe(OmpModelsWriteStatus.Written, removed.Message);
        var after = await configuration.Catalog.ListAsync(cancellationToken).ConfigureAwait(true);
        after.Models.ShouldNotContain(model => model.ProviderId == "autonogrammer-live");
    }

    [Fact]
    public async Task AChangeTheRealOmpRejectsIsRolledBackByteForByte()
    {
        using var sandbox = ArrangeSandbox();
        // A hand-written provider without apiKey or auth: omp 18.12.6 disables custom providers and warns about it on stderr
        // while still exiting 0. Autonogrammer must treat that warning as a rejection and restore the file.
        const string handWritten =
            "# hand-written, and broken\nproviders:\n  broken:\n    baseUrl: http://127.0.0.1:1/v1\n    api: openai-completions\n    models:\n      - id: m\n";
        await File.WriteAllTextAsync(sandbox.ModelsYmlPath, handWritten, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var configuration = new OmpModelConfiguration(sandbox.Options());

        var result = await configuration.ModelsFile.UpsertAsync(LiveProvider("autonogrammer-live"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        SkipWhenOmpCouldNotAnswer(result);

        result.Status.ShouldBe(OmpModelsWriteStatus.Rejected, result.Message);
        result.Message.ShouldContain("restored", Case.Sensitive);
        (await File.ReadAllTextAsync(sandbox.ModelsYmlPath, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(handWritten);
    }

    [Fact]
    public async Task AnImportedKeyIsResolvedByOmpAndKeptOwnerOnly()
    {
        using var sandbox = ArrangeSandbox();
        var configuration = new OmpModelConfiguration(sandbox.Options());
        var cancellationToken = TestContext.Current.CancellationToken;
        const string providerId = "autonogrammer-live-import";

        var before = await configuration.Credentials.GetPresenceAsync(providerId, cancellationToken).ConfigureAwait(true);
        before.ShouldBe(OmpCredentialPresence.Absent);

        var imported = await configuration.Credentials.ImportApiKeyAsync(providerId, new SecretString(LiveOmpSandbox.FakeKey), cancellationToken).ConfigureAwait(true);

        imported.Status.ShouldBe(OmpCredentialImportStatus.Imported, imported.Message);
        imported.Message.ShouldNotContain(LiveOmpSandbox.FakeKey, Case.Sensitive);
        (await configuration.Credentials.GetPresenceAsync(providerId, cancellationToken).ConfigureAwait(true)).ShouldBe(OmpCredentialPresence.Present);

        var database = Path.Combine(sandbox.AgentDirectory, "agent.db");
        File.Exists(database).ShouldBeTrue("omp keeps imported keys in agent.db, as OmpCredentialStore.StorageNote says.");
        (File.GetUnixFileMode(database) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite))
            .ShouldBe(UnixFileMode.None, "agent.db must be readable by its owner only.");
    }

    [Fact]
    public async Task SettingTheDefaultModelKeepsTheOtherRolesInTheRealOmp()
    {
        using var sandbox = ArrangeSandbox();
        var configuration = new OmpModelConfiguration(sandbox.Options());
        var cancellationToken = TestContext.Current.CancellationToken;
        var written = await configuration.ModelsFile.UpsertAsync(LiveProvider("autonogrammer-live"), cancellationToken).ConfigureAwait(true);
        SkipWhenOmpCouldNotAnswer(written);
        var listing = await configuration.Catalog.ListAsync(cancellationToken).ConfigureAwait(true);
        SkipWhenListingIsIncomplete(listing);

        var seeded = await configuration.Runner.RunAsync(
            new OmpCommandRequest(["config", "set", "modelRoles", """{"smol":"autonogrammer-live/live-small","slow":"autonogrammer-live/live-small"}"""]),
            cancellationToken).ConfigureAwait(true);
        seeded.Succeeded.ShouldBeTrue("seeding modelRoles");

        var result = await configuration.DefaultModel.SetDefaultAsync("autonogrammer-live/live-small", cancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.Set, result.Message);
        var roles = await configuration.DefaultModel.GetAsync(cancellationToken).ConfigureAwait(true);
        roles.WasRead.ShouldBeTrue(roles.Problem);
        roles.DefaultSelector.ShouldBe("autonogrammer-live/live-small");
        roles.Roles["smol"].ShouldBe("autonogrammer-live/live-small");
        roles.Roles["slow"].ShouldBe("autonogrammer-live/live-small");

        var unknown = await configuration.DefaultModel.SetDefaultAsync("autonogrammer-live/not-listed", cancellationToken).ConfigureAwait(true);
        unknown.Status.ShouldBe(OmpDefaultModelStatus.UnknownModel);
        (await configuration.DefaultModel.GetAsync(cancellationToken).ConfigureAwait(true)).DefaultSelector.ShouldBe("autonogrammer-live/live-small");
    }

    private static OmpManagedProvider LiveProvider(string id) =>
        new(
            id,
            new Uri("http://127.0.0.1:11434/v1"),
            OmpProviderApi.OpenAiCompletions,
            ApiKeyEnvName: null,
            NoAuthentication: true,
            [new OmpManagedModel("live-small", "Live Small", ContextWindow: 8192, MaxTokens: 2048)]);

    private static LiveOmpSandbox ArrangeSandbox()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the live omp configuration tests against the installed omp.");
        }

        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The live omp configuration tests check Unix file permissions.");
        }

        if (LiveOmpSandbox.FindOmp() is null)
        {
            Assert.Skip("omp is not installed; the live omp configuration tests are skipped.");
        }

        return new LiveOmpSandbox();
    }

    private static void SkipWhenOmpCouldNotAnswer(OmpModelsWriteResult result)
    {
        if (result.Status != OmpModelsWriteStatus.Failed)
        {
            return;
        }

        var reason = $"omp could not validate models.yml in the sandbox (a cold model cache may need the network): {result.Message}";
        TestContext.Current.TestOutputHelper?.WriteLine(reason);
        Assert.Skip(reason);
    }

    private static void SkipWhenListingIsIncomplete(OmpModelListing listing)
    {
        if (listing.IsComplete)
        {
            return;
        }

        var reason = $"omp could not list its models in the sandbox (a cold model cache may need the network); the listing assertions were skipped: {listing.Warning}";
        TestContext.Current.TestOutputHelper?.WriteLine(reason);
        Assert.Skip(reason);
    }
}
