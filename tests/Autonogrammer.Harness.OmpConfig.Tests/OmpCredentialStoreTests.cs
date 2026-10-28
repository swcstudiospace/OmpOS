using System.Text.Json;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpCredentialStoreTests
{
    private const string Key = "sk-fake-0000-test-VERY-UNIQUE-KEY-TEXT";

    [Theory]
    [InlineData(0, OmpCredentialPresence.Present)]
    [InlineData(1, OmpCredentialPresence.Absent)]
    [InlineData(2, OmpCredentialPresence.Unknown)]
    [InlineData(127, OmpCredentialPresence.Unknown)]
    public async Task MapsTheExitCodeOfOmpTokenToPresence(int exitCode, OmpCredentialPresence expected)
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetTokenExit("acme", exitCode);
        var store = new OmpCredentialStore(fake.Options());

        var presence = await store.GetPresenceAsync("acme", TestContext.Current.CancellationToken).ConfigureAwait(true);

        presence.ShouldBe(expected);
        fake.Calls().Single().Arguments.ShouldBe(["token", "acme"]);
    }

    [Fact]
    public async Task AMissingOmpIsUnknownNotAbsent()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.OptionsWithoutOmp());

        var presence = await store.GetPresenceAsync("acme", TestContext.Current.CancellationToken).ConfigureAwait(true);

        presence.ShouldBe(OmpCredentialPresence.Unknown);
    }

    [Fact]
    public async Task ATimedOutCheckIsUnknown()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetTokenHangs("acme");
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
        var store = new OmpCredentialStore(fake.Options(clock, TimeSpan.FromSeconds(5)));

        var check = store.GetPresenceAsync("acme", TestContext.Current.CancellationToken);
        while (clock.Timers.Count == 0)
        {
            await Task.Yield();
        }

        clock.Timers.Single().Fire();
        var presence = await check.ConfigureAwait(true);

        presence.ShouldBe(OmpCredentialPresence.Unknown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("a b")]
    [InlineData("--help")]
    [InlineData("-x")]
    [InlineData("name\n")]
    [InlineData("../escape")]
    public async Task AnInvalidProviderIdStartsNoProcess(string providerId)
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.Options());

        var presence = await store.GetPresenceAsync(providerId, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var import = await store.ImportApiKeyAsync(providerId, new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        presence.ShouldBe(OmpCredentialPresence.Unknown);
        import.Status.ShouldBe(OmpCredentialImportStatus.Failed);
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportSendsExactlyTheDocumentOnStandardInputAndNeverOnTheCommandLine()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.Options());

        var result = await store.ImportApiKeyAsync("acme", new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpCredentialImportStatus.Imported);
        var calls = fake.Calls();
        calls.Count.ShouldBe(2);
        calls[0].Arguments.ShouldBe(["auth", "import", "-"]);
        calls.SelectMany(call => call.Arguments).ShouldNotContain(argument => argument.Contains(Key, StringComparison.Ordinal));
        calls.SelectMany(call => call.Environment).ShouldNotContain(line => line.Contains(Key, StringComparison.Ordinal));

        using var document = JsonDocument.Parse(calls[0].Stdin!);
        var root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).ShouldBe(["version", "credentials"]);
        root.GetProperty("version").GetInt32().ShouldBe(1);
        var credentials = root.GetProperty("credentials");
        credentials.GetArrayLength().ShouldBe(1);
        var entry = credentials[0];
        entry.EnumerateObject().Select(property => property.Name).ShouldBe(["provider", "credential"]);
        entry.GetProperty("provider").GetString().ShouldBe("acme");
        var credential = entry.GetProperty("credential");
        credential.EnumerateObject().Select(property => property.Name).ShouldBe(["type", "key"]);
        credential.GetProperty("type").GetString().ShouldBe("api_key");
        credential.GetProperty("key").GetString().ShouldBe(Key);
    }

    [Fact]
    public async Task ImportVerifiesWithTokenAfterTheImport()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.Options());

        _ = await store.ImportApiKeyAsync("acme", new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        fake.Calls()[1].Arguments.ShouldBe(["token", "acme"]);
    }

    [Fact]
    public async Task ImportEscapesAKeyThatNeedsJsonEscaping()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.Options());
        const string awkward = "key-with-\"quote\"-and-\\backslash-and-\u00e9-and-</script>";

        var result = await store.ImportApiKeyAsync("acme", new SecretString(awkward), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsImported.ShouldBeTrue();
        using var document = JsonDocument.Parse(fake.Calls()[0].Stdin!);
        document.RootElement.GetProperty("credentials")[0].GetProperty("credential").GetProperty("key").GetString().ShouldBe(awkward);
    }

    [Fact]
    public async Task AnImportThatOmpRefusesFailsWithoutTheKeyAnywhere()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetAuthExit(1);
        var store = new OmpCredentialStore(fake.Options());

        var result = await store.ImportApiKeyAsync("acme", new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpCredentialImportStatus.Failed);
        result.Message.ShouldNotContain(Key, Case.Sensitive);
        result.ToString().ShouldNotContain(Key, Case.Sensitive);
        fake.Calls().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnImportOmpAcceptsButCannotResolveIsReportedAsFailed()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetAuthStoresNothing();
        var store = new OmpCredentialStore(fake.Options());

        var result = await store.ImportApiKeyAsync("acme", new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpCredentialImportStatus.Failed);
        result.Message.ShouldNotContain(Key, Case.Sensitive);
        fake.Calls().Count.ShouldBe(2);
    }

    [Fact]
    public async Task AMissingOmpFailsTheImportWithoutTheKeyInTheMessage()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.OptionsWithoutOmp());

        var result = await store.ImportApiKeyAsync("acme", new SecretString(Key), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpCredentialImportStatus.Failed);
        result.Message.ShouldNotContain(Key, Case.Sensitive);
    }

    [Fact]
    public async Task ImportRejectsANullKey()
    {
        using var fake = new FakeOmpEnvironment();
        var store = new OmpCredentialStore(fake.Options());

        _ = await Should.ThrowAsync<ArgumentNullException>(() => store.ImportApiKeyAsync("acme", null!, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public void TheStorageNoteStatesWhereTheKeyGoesAndHowItIsProtected()
    {
        OmpCredentialStore.StorageNote.ShouldBe(
            "omp keeps this key in its own credential database (agent.db) with owner-only permissions, unencrypted; Autonogrammer keeps no copy.");
    }
}
