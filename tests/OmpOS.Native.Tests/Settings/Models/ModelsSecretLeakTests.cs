using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// A canary key goes through every action of the Models source, successful and failing, and every output is scanned: the results and
/// their messages, the snapshots, the outcomes of tests, the renderings of the source objects, and every file the flows wrote except
/// the OmpOS key store, which holds a custom provider's key by design. The scan is proved able to see the canary by a control that
/// reads the key store itself.
/// </summary>
public sealed class ModelsSecretLeakTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NoOutputOfAnyActionCarriesTheKeyAndTheOnlyFileThatHoldsItIsTheKeyStore()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        fake.SetModels(ModelsFakeOmp.Row("groq", "m1"));
        using var composition = fake.Compose();
        var source = composition.Source;
        var texts = new List<string>();

        void Collect(ModelsActionResult result) => texts.Add(result.Message ?? string.Empty);

        // A key for a built-in provider: refused, then accepted.
        fake.SetAuthExit(2);
        Collect(await source.SaveApiKeyAsync("groq", ModelsForms.Canary, Token).ConfigureAwait(true));
        File.Delete(Path.Combine(fake.FakeDirectory, "auth.exit"));
        Collect(await source.SaveApiKeyAsync("groq", ModelsForms.Canary, Token).ConfigureAwait(true));
        Collect(await source.SaveApiKeyAsync("made-up", ModelsForms.Canary, Token).ConfigureAwait(true));
        Collect(await source.SaveApiKeyAsync("groq", "bad key with spaces " + ModelsForms.Canary, Token).ConfigureAwait(true));

        // A custom provider with a key: saved, edited, tested, failing to save, removed.
        Collect(await source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true));
        Collect(await source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true));
        Collect(await source.SaveCustomProviderAsync(ModelsForms.Gateway("Bad Id"), Token).ConfigureAwait(true));
        Collect(await source.SaveCustomProviderAsync(ModelsForms.Gateway("openai"), Token).ConfigureAwait(true));
        Collect(await source.SetDefaultModelAsync("groq/m1", Token).ConfigureAwait(true));
        Collect(await source.SetDefaultModelAsync("groq/missing", Token).ConfigureAwait(true));
        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);
        texts.Add(snapshot.Warning ?? string.Empty);
        texts.AddRange(snapshot.Providers.Select(static provider => provider.ToString()));
        texts.AddRange(snapshot.Providers.SelectMany(static provider => provider.Models));
        texts.AddRange(snapshot.Templates.Select(static template => template.ToString()));
        foreach (var provider in new[] { "gateway", "groq", "openai" })
        {
            texts.Add((await source.TestAsync(provider, Token).ConfigureAwait(true)).ToString());
        }

        texts.Add(source.ToString()!);
        texts.Add(composition.Keys.ToString());
        var overlay = composition.Keys.Overlay(new Dictionary<string, string>(StringComparer.Ordinal));
        texts.Add(overlay.ToString());
        var keyStoreText = await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true);

        Collect(await source.RemoveCustomProviderAsync("gateway", Token).ConfigureAwait(true));

        texts.ShouldAllBe(text => !text.Contains(ModelsForms.Canary, StringComparison.Ordinal));
        foreach (var (path, content) in fake.ReadEverything(fake.SecretsPath))
        {
            content.ShouldNotContain(ModelsForms.Canary, Case.Sensitive, path);
        }

        keyStoreText.ShouldContain(ModelsForms.Canary, Case.Sensitive, "control: the scan can see the canary where it is stored");
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        fake.Calls().SelectMany(static call => call.Arguments).ShouldNotContain(argument => argument.Contains(ModelsForms.Canary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACorruptKeyStoreIsReportedWithoutItsContents()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var composition = fake.Compose();
        (await composition.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fake.SecretsPath)!);
        await File.WriteAllTextAsync(fake.SecretsPath, "{\"version\":1,\"secrets\":{\"X\":\"" + ModelsForms.Canary + "\",", Token).ConfigureAwait(true);

        var snapshot = await composition.Source.LoadAsync(Token).ConfigureAwait(true);
        var save = await composition.Source.SaveApiKeyAsync("gateway", "another-key-value", Token).ConfigureAwait(true);
        var remove = await composition.Source.RemoveCustomProviderAsync("gateway", Token).ConfigureAwait(true);

        snapshot.Warning.ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        save.Succeeded.ShouldBeFalse();
        save.Message.ShouldNotBeNull().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        remove.Succeeded.ShouldBeFalse("the provider was removed from omp but the store could not be changed, and the user is told");
        remove.Message.ShouldNotBeNull().ShouldContain("could not delete its stored key", Case.Sensitive);
        remove.Message.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        (await File.ReadAllTextAsync(fake.SecretsPath, Token).ConfigureAwait(true)).ShouldContain(ModelsForms.Canary, Case.Sensitive);
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }
}
