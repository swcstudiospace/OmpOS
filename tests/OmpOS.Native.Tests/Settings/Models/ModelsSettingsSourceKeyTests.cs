using System.Text.Json;
using OmpOS.Harness.Secrets;
using OmpOS.Native.Settings.Models;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// The key and default model actions of <see cref="ModelsSettingsSource"/> through a fake omp: a built-in provider's key goes to omp on
/// standard input and OmpOS keeps no copy; a custom provider's key goes to the OmpOS key store under its variable name; the default
/// model is the <c>default</c> role of <c>modelRoles</c>. No output carries the key.
/// </summary>
public sealed class ModelsSettingsSourceKeyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AKeyForABuiltInProviderTravelsOnStandardInputAndNowhereElse()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var result = await composition.Source.SaveApiKeyAsync("groq", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        var calls = fake.Calls();
        var import = calls.Single(call => string.Equals(call.Arguments[0], "auth", StringComparison.Ordinal));
        import.Arguments.ShouldBe(["auth", "import", "-"]);
        import.StandardInput.ShouldNotBeNull().ShouldContain(ModelsForms.Canary, Case.Sensitive);
        import.StandardInput.ShouldContain("\"provider\":\"groq\"", Case.Sensitive);
        calls.SelectMany(static call => call.Arguments).ShouldNotContain(argument => argument.Contains(ModelsForms.Canary, StringComparison.Ordinal));
        calls.SelectMany(static call => call.Environment).ShouldNotContain(line => line.Contains(ModelsForms.Canary, StringComparison.Ordinal));
        File.Exists(fake.SecretsPath).ShouldBeFalse("OmpOS keeps no copy of a key omp stores itself");
        result.Message.ShouldBeNull();

        var reloaded = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        reloaded.Providers.Single(provider => string.Equals(provider.Id, "groq", StringComparison.Ordinal)).Credential.ShouldBe(CredentialState.Present);
    }

    [Fact]
    public async Task ARefusedImportIsAPlainFailureWithoutTheKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetAuthExit(3);
        using var composition = fake.Compose();

        var result = await composition.Source.SaveApiKeyAsync("groq", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("refused", Case.Sensitive);
        result.Message.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
    }

    [Fact]
    public async Task AnUnknownProviderWhoseCredentialOmpDoesNotResolveIsAFailureNotASuccess()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetAuthStoresNothing();
        using var composition = fake.Compose();

        var result = await composition.Source.SaveApiKeyAsync("made-up", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("does not report a credential", Case.Sensitive);
        result.Message.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two words")]
    [InlineData("line\nbreak")]
    public async Task AKeyThatCannotBePassedOnIsRefusedBeforeOmpIsStarted(string key)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var result = await composition.Source.SaveApiKeyAsync("groq", key, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ANotValidProviderIdIsRefusedBeforeOmpIsStarted()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();

        var result = await composition.Source.SaveApiKeyAsync("Not Valid", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task TheKeyOfACustomProviderIsKeptInTheOmpOsStoreUnderItsVariableAndNotGivenToOmp()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var result = await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(fake.SecretsPath, Token).ConfigureAwait(true));
        document.RootElement.GetProperty("secrets").GetProperty("OMPOS_PROVIDER_GATEWAY_KEY").GetString().ShouldBe(ModelsForms.Canary);
        fake.Calls().ShouldNotContain(call => call.Arguments[0] == "auth", "omp cannot use an imported key for a models.yml provider");
        fake.ReadModelsYml().ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        composition.Keys.Current.Keys.ShouldBe(["OMPOS_PROVIDER_GATEWAY_KEY"]);
        composition.Keys.Current["OMPOS_PROVIDER_GATEWAY_KEY"].ShouldBe(ModelsForms.Canary);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(fake.SecretsPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.GetUnixFileMode(Path.GetDirectoryName(fake.SecretsPath)!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task AKeylessCustomProviderTakesNoKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Local("acme"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var result = await composition.Source.SaveApiKeyAsync("acme", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("need no key", Case.Sensitive);
        File.Exists(fake.SecretsPath).ShouldBeFalse();
    }

    [Fact]
    public async Task AKeyThatTheStoreCannotWriteIsAFailureWithoutTheKeyAndTheStoreIsLeftAlone()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fake.SecretsPath)!);
        await File.WriteAllTextAsync(fake.SecretsPath, "corrupt", Token).ConfigureAwait(true);

        var result = await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("could not store the key", Case.Sensitive);
        result.Message.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldBe("corrupt");
        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        snapshot.Providers.Single(provider => string.Equals(provider.Id, "gateway", StringComparison.Ordinal)).Credential.ShouldBe(CredentialState.Unknown);
        snapshot.Warning.ShouldNotBeNull().ShouldContain("secret file", Case.Sensitive);
    }

    [Fact]
    public async Task SettingTheDefaultModelWritesTheDefaultRoleAndKeepsTheOtherRoles()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(ModelsFakeOmp.Row("openai", "gpt-x"), ModelsFakeOmp.Row("openai", "gpt-y"));
        fake.SetRoles("{\"smol\":\"openai/gpt-y\"}");
        using var composition = fake.Compose();

        var result = await composition.Source.SetDefaultModelAsync("openai/gpt-x", Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        using var roles = JsonDocument.Parse(fake.ReadRoles()!);
        roles.RootElement.GetProperty("default").GetString().ShouldBe("openai/gpt-x");
        roles.RootElement.GetProperty("smol").GetString().ShouldBe("openai/gpt-y");
        (await composition.Source.LoadAsync(Token).ConfigureAwait(true)).DefaultModel.ShouldBe("openai/gpt-x");
    }

    [Theory]
    [InlineData("openai/not-listed", "does not list")]
    [InlineData("no-slash", "provider/model-id")]
    public async Task ADefaultModelOmpDoesNotListIsAPlainFailureAndNothingIsWritten(string selector, string reason)
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(ModelsFakeOmp.Row("openai", "gpt-x"));
        using var composition = fake.Compose();

        var result = await composition.Source.SetDefaultModelAsync(selector, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain(reason, Case.Sensitive);
        fake.ReadRoles().ShouldBeNull();
    }

    [Fact]
    public async Task APlainFailureOfTheStoreNeverQuotesAPathOrTheKey()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose(secrets: new RefusingSecretStore());
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var result = await composition.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        result.Message.ShouldNotContain(fake.Root, Case.Sensitive);
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }

    /// <summary>A key store that fails every write the way a full disk does, with a message that has no value in it.</summary>
    private sealed class RefusingSecretStore : ISecretStore
    {
        public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken) =>
            Task.FromException(new SecretStoreException("The secret file could not be written. The previous contents are unchanged."));

        public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken) => Task.FromResult<SecretString?>(null);

        public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
