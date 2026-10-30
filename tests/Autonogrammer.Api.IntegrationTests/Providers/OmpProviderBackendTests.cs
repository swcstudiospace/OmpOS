using System.Text;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Harness.OmpConfig;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>
/// The default provider backend over a POSIX shell script that stands in for omp: it lists models, answers <c>omp token</c>, takes an import on
/// standard input, keeps the <c>modelRoles</c> setting and loads nothing else. No real omp, model or network is involved.
/// </summary>
public sealed class OmpProviderBackendTests : IDisposable
{
    private const string Canary = "sk-canary-backend-key-77aa22bb33cc";

    private const string Script =
        """
        #!/bin/sh
        D="__DIR__"
        if [ "$1" = "version" ]; then echo "omp 18.12.6-fake"; exit 0; fi
        case "$1" in
          models)
            if [ -f "$D/models.json" ]; then cat "$D/models.json"; else echo '{"models":[]}'; fi
            exit 0 ;;
          token)
            if [ -f "$D/token.$2" ]; then echo "tok-discarded"; exit 0; fi
            exit 1 ;;
          auth)
            cat > "$D/stdin"
            provider=$(sed -n 's/.*"provider":"\([^"]*\)".*/\1/p' "$D/stdin")
            echo 0 > "$D/token.$provider"
            exit 0 ;;
          config)
            if [ "$2" = get ]; then
              if [ -f "$D/roles.json" ]; then cat "$D/roles.json"; else echo '{}'; fi
              exit 0
            fi
            if [ "$2" = set ]; then printf '%s' "$4" > "$D/roles.json"; exit 0; fi ;;
        esac
        exit 2
        """;

    private readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-api-providers-" + Guid.NewGuid().ToString("N")));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string FakeDirectory => Path.Combine(_root, "fake");

    private string AgentDirectory => Path.Combine(_root, "agent");

    private string ModelsYmlPath => Path.Combine(AgentDirectory, "models.yml");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static OmpManagedProvider Block(string id = "mine") =>
        new(id, new Uri("https://llm.example.com/v1"), OmpProviderApi.OpenAiCompletions, "AUTONOGRAMMER_PROVIDER_MINE_KEY", NoAuthentication: false, [new OmpManagedModel("m1")]);

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }

    private OmpProviderBackend Create()
    {
        _ = Directory.CreateDirectory(FakeDirectory);
        _ = Directory.CreateDirectory(AgentDirectory);
        var path = Path.Combine(FakeDirectory, "omp");
        File.WriteAllText(path, Script.Replace("__DIR__", FakeDirectory, StringComparison.Ordinal).ReplaceLineEndings("\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new OmpProviderBackend(
            Microsoft.Extensions.Options.Options.Create(new ProvidersOptions { OmpPath = path, AgentDirectory = AgentDirectory }),
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions()),
            TimeProvider.System);
    }

    [Fact]
    public async Task TheStateHoldsTheProvidersOmpListsTheBlocksAndTheDefaultModel()
    {
        SkipUnlessLinux();
        using var backend = Create();
        await File.WriteAllTextAsync(Path.Combine(FakeDirectory, "models.json"), """{"models":[{"provider":"groq","kind":"chat","id":"m1","selector":"groq/m1","name":"m1","contextWindow":1000,"maxTokens":100,"reasoning":false,"input":["text"]}]}""", Token).ConfigureAwait(true);
        await File.WriteAllTextAsync(Path.Combine(FakeDirectory, "roles.json"), """{"default":"groq/m1"}""", Token).ConfigureAwait(true);

        var state = await backend.ReadStateAsync(Token).ConfigureAwait(true);

        state.OmpInstalled.ShouldBeTrue();
        state.Complete.ShouldBeTrue();
        state.CredentialedProviderIds.ShouldBe(["groq"]);
        state.DefaultModel.ShouldBe("groq/m1");
        state.Blocks.ShouldBeEmpty();
    }

    [Fact]
    public async Task ABlockIsWrittenReadBackAndRemovedAndNothingElseOfTheFileChanges()
    {
        SkipUnlessLinux();
        using var backend = Create();
        const string Existing = "# kept\nproviders:\n";
        await File.WriteAllTextAsync(ModelsYmlPath, Existing, Token).ConfigureAwait(true);

        var written = await backend.UpsertBlockAsync(Block(), Token).ConfigureAwait(true);
        var state = await backend.ReadStateAsync(Token).ConfigureAwait(true);
        var yml = await File.ReadAllTextAsync(ModelsYmlPath, Token).ConfigureAwait(true);

        written.IsDone.ShouldBeTrue();
        state.Blocks["mine"].ShouldBe("AUTONOGRAMMER_PROVIDER_MINE_KEY");
        yml.ShouldContain("# kept", Case.Sensitive);
        yml.ShouldContain("apiKey: \"AUTONOGRAMMER_PROVIDER_MINE_KEY\"", Case.Sensitive);

        var removed = await backend.RemoveBlockAsync("mine", Token).ConfigureAwait(true);

        removed.IsDone.ShouldBeTrue();
        (await File.ReadAllTextAsync(ModelsYmlPath, Token).ConfigureAwait(true)).ShouldNotContain("mine", Case.Sensitive);
    }

    [Fact]
    public async Task AProviderOutsideAnAutonogrammerBlockIsAConflictAndTheFileIsUntouched()
    {
        SkipUnlessLinux();
        using var backend = Create();
        const string Existing = "# mine\nproviders:\n  mine:\n    baseUrl: x\n";
        await File.WriteAllTextAsync(ModelsYmlPath, Existing, Token).ConfigureAwait(true);

        var result = await backend.UpsertBlockAsync(Block(), Token).ConfigureAwait(true);

        result.Status.ShouldBe(ProviderWriteStatus.Conflict);
        result.Message.ShouldNotBeNull().ShouldNotContain(_root, Case.Sensitive);
        (await File.ReadAllTextAsync(ModelsYmlPath, Token).ConfigureAwait(true)).ShouldBe(Existing);
    }

    [Fact]
    public async Task AKeyIsHandedToOmpOnStandardInputAndTheResultHasNoKey()
    {
        SkipUnlessLinux();
        using var backend = Create();

        var result = await backend.ImportCredentialAsync("groq", Canary, Token).ConfigureAwait(true);

        result.IsDone.ShouldBeTrue();
        result.Message.ShouldBeNull();
        (await File.ReadAllTextAsync(Path.Combine(FakeDirectory, "stdin"), Token).ConfigureAwait(true)).ShouldContain(Canary, Case.Sensitive);
        Directory.EnumerateFiles(AgentDirectory, "*", SearchOption.AllDirectories).ShouldAllBe(path => !File.ReadAllText(path).Contains(Canary, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDefaultModelIsSetOnlyForAModelOmpLists()
    {
        SkipUnlessLinux();
        using var backend = Create();
        await File.WriteAllTextAsync(Path.Combine(FakeDirectory, "models.json"), """{"models":[{"provider":"groq","kind":"chat","id":"m1","selector":"groq/m1","name":"m1","contextWindow":1000,"maxTokens":100,"reasoning":false,"input":["text"]}]}""", Token).ConfigureAwait(true);

        var done = await backend.SetDefaultModelAsync("groq/m1", Token).ConfigureAwait(true);
        var refused = await backend.SetDefaultModelAsync("groq/missing", Token).ConfigureAwait(true);

        done.IsDone.ShouldBeTrue();
        refused.Status.ShouldBe(ProviderWriteStatus.Rejected);
        (await File.ReadAllTextAsync(Path.Combine(FakeDirectory, "roles.json"), Token).ConfigureAwait(true)).ShouldContain("groq/m1", Case.Sensitive);
    }
}
