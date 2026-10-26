using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpConfig.Tests;

[UnsupportedOSPlatform("windows")]
public sealed class OmpModelsFileTests
{
    private const string Original = "# my models\nproviders:\n  # a hand-written provider\n  mine:\n    baseUrl: http://127.0.0.1:1/v1\n    apiKey: MINE_KEY\n\nmodelRoles:\n  default: mine/m\n";

    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreatesTheFileWithProvidersAndOwnerOnlyPermissions()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Written);
        Text(fake.ReadModelsYml()).ShouldBe(OmpModelsEditor.Upsert(string.Empty, OmpModelsFixtures.Local()).NewText);
        File.GetUnixFileMode(fake.ModelsYmlPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        fake.Backups().ShouldBeEmpty();
        fake.AgentFiles().ShouldBe(["models.yml"]);
    }

    [Fact]
    public async Task ValidatesWithTheRealCommandAfterTheWrite()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());

        _ = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var call = fake.Calls().Single();
        call.Arguments.ShouldBe(["models", "--json", "--no-extensions"]);
        call.Environment.ShouldContain($"PI_CODING_AGENT_DIR={fake.AgentDirectory}", StringComparer.Ordinal);
    }

    [Fact]
    public async Task KeepsEverythingOutsideTheBlockAndABackupOfThePreviousFile()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var clock = new ManualTimeProvider(Start);
        var file = new OmpModelsFile(fake.Options(clock));

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Written);
        var written = Text(fake.ReadModelsYml());
        written.ShouldStartWith("# my models\nproviders:\n  # a hand-written provider\n  mine:\n    baseUrl: http://127.0.0.1:1/v1\n    apiKey: MINE_KEY\n", Case.Sensitive);
        written.ShouldEndWith("\nmodelRoles:\n  default: mine/m\n", Case.Sensitive);
        fake.Backups().ShouldBe(["models.yml.bak-ompos-20261026T080000Z"]);
        Text(fake.ReadAgentFile("models.yml.bak-ompos-20261026T080000Z")).ShouldBe(Original);
        File.GetUnixFileMode(Path.Combine(fake.AgentDirectory, "models.yml.bak-ompos-20261026T080000Z")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task AnUnchangedProviderWritesNothingAndStartsNoProcess()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());
        _ = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var before = fake.ReadModelsYml();

        var again = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        again.Status.ShouldBe(OmpModelsWriteStatus.Unchanged);
        again.IsSatisfied.ShouldBeTrue();
        fake.ReadModelsYml().ShouldBe(before!);
        fake.Backups().ShouldBeEmpty();
        fake.Calls().Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveRestoresTheOriginalBytesExactly()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options());
        _ = await file.UpsertAsync(OmpModelsFixtures.Keyed(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var removed = await file.RemoveAsync("gateway", TestContext.Current.CancellationToken).ConfigureAwait(true);

        removed.Status.ShouldBe(OmpModelsWriteStatus.Written);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
    }

    [Fact]
    public async Task RemovingAMissingProviderIsUnchangedAndStartsNoProcess()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options());

        var removed = await file.RemoveAsync("gateway", TestContext.Current.CancellationToken).ConfigureAwait(true);

        removed.Status.ShouldBe(OmpModelsWriteStatus.Unchanged);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
        fake.Calls().ShouldBeEmpty();
        fake.Backups().ShouldBeEmpty();
    }

    [Fact]
    public async Task BackupsRotateAtFiveKeepingTheNewest()
    {
        using var fake = new FakeOmpEnvironment();
        var clock = new ManualTimeProvider(Start);
        var file = new OmpModelsFile(fake.Options(clock));

        for (var write = 1; write <= 8; write++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            var provider = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("m" + write.ToString(CultureInfo.InvariantCulture))] };
            var result = await file.UpsertAsync(provider, TestContext.Current.CancellationToken).ConfigureAwait(true);
            result.Status.ShouldBe(OmpModelsWriteStatus.Written);
        }

        // Write 1 created the file (no backup). Writes 2..8 each backed up the previous file; the newest five are kept.
        fake.Backups().ShouldBe(
        [
            "models.yml.bak-ompos-20261026T080004Z",
            "models.yml.bak-ompos-20261026T080005Z",
            "models.yml.bak-ompos-20261026T080006Z",
            "models.yml.bak-ompos-20261026T080007Z",
            "models.yml.bak-ompos-20261026T080008Z",
        ]);
    }

    [Fact]
    public async Task TwoWritesInOneSecondKeepBothBackups()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options(new ManualTimeProvider(Start)));

        _ = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var afterFirst = fake.ReadModelsYml();
        _ = await file.UpsertAsync(OmpModelsFixtures.Local() with { BaseUrl = new Uri("http://127.0.0.1:9/v1") }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        fake.Backups().ShouldBe(["models.yml.bak-ompos-20261026T080000Z", "models.yml.bak-ompos-20261026T080000Z-2"]);
        Text(fake.ReadAgentFile("models.yml.bak-ompos-20261026T080000Z")).ShouldBe(Original);
        fake.ReadAgentFile("models.yml.bak-ompos-20261026T080000Z-2").ShouldBe(afterFirst!);
    }

    [Fact]
    public async Task WhenOmpWarnsTheFileIsRolledBackToTheBytesItHad()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options(new ManualTimeProvider(Start)));
        var broken = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("BADMODEL")] };

        var result = await file.UpsertAsync(broken, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Rejected);
        result.Message.ShouldContain("Provider bad: nope", Case.Sensitive);
        result.Message.ShouldContain("restored", Case.Sensitive);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
        fake.AgentFiles().Where(name => name.EndsWith(".tmp", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenOmpWarnsAboutAFileThatDidNotExistTheFileIsRemoved()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());
        var broken = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("BADMODEL")] };

        var result = await file.UpsertAsync(broken, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Rejected);
        File.Exists(fake.ModelsYmlPath).ShouldBeFalse();
    }

    [Fact]
    public async Task WhenOmpCannotBeAskedTheChangeIsRevertedAndReportedAsFailed()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.OptionsWithoutOmp());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Failed);
        result.IsSatisfied.ShouldBeFalse();
        Text(fake.ReadModelsYml()).ShouldBe(Original);
    }

    [Fact]
    public async Task ATimedOutValidationRevertsTheChange()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        fake.SetModelsHangs();
        var clock = new ManualTimeProvider(Start);
        var file = new OmpModelsFile(fake.Options(clock, TimeSpan.FromSeconds(5)));

        var upsert = file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken);
        while (clock.Timers.Count == 0)
        {
            await Task.Yield();
        }

        clock.Timers.Single().Fire();
        var result = await upsert.ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Failed);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
    }

    [Fact]
    public async Task CancellingDuringValidationRestoresThePreviousFileAndThrows()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        fake.SetModelsHangs();
        var file = new OmpModelsFile(fake.Options());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var upsert = file.UpsertAsync(OmpModelsFixtures.Local(), cancellation.Token);
        while (!fake.HasStarted())
        {
            await Task.Yield();
        }

        await cancellation.CancelAsync().ConfigureAwait(true);

        OperationCanceledException? thrown = null;
        try
        {
            _ = await upsert.ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        thrown.ShouldNotBeNull();
        Text(fake.ReadModelsYml()).ShouldBe(Original);
    }

    [Fact]
    public async Task ARemovalIsNeverRolledBackBecauseOmpStillWarnsAboutOtherEntries()
    {
        using var fake = new FakeOmpEnvironment();
        var withBlock = OmpModelsEditor.Upsert("providers:\n  other:\n    note: x\n", OmpModelsFixtures.Local()).NewText!;
        fake.WriteModelsYml(withBlock.Replace("note: x", "note: BADMODEL", StringComparison.Ordinal));
        var foreign = "providers:\n  other:\n    note: BADMODEL\n";
        var file = new OmpModelsFile(fake.Options());

        var removed = await file.RemoveAsync("acme", TestContext.Current.CancellationToken).ConfigureAwait(true);

        removed.Status.ShouldBe(OmpModelsWriteStatus.Written);
        removed.Message.ShouldContain("could not confirm", Case.Sensitive);
        Text(fake.ReadModelsYml()).ShouldBe(foreign);
    }

    [Fact]
    public async Task AnIdThatExistsOutsideABlockIsAConflictAndTheFileIsUntouched()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local("mine"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Conflict);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
        fake.Backups().ShouldBeEmpty();
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AFlowStyleProvidersMappingIsRefusedAndTheFileIsUntouched()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml("providers: {}\n");
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        result.Message.ShouldContain("by hand", Case.Insensitive);
        Text(fake.ReadModelsYml()).ShouldBe("providers: {}\n");
        fake.Backups().ShouldBeEmpty();
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnExistingModelsYamlMakesTheEditorStepAside()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteAgentFile("models.yaml", "providers:\n");
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        File.Exists(fake.ModelsYmlPath).ShouldBeFalse();
    }

    [Fact]
    public async Task AFileThatIsNotUtf8IsRefusedUntouched()
    {
        using var fake = new FakeOmpEnvironment();
        byte[] invalid = [(byte)'#', (byte)' ', 0xFF, 0xFE, 0xC3, (byte)'\n'];
        fake.WriteModelsYml(invalid);
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var read = await file.ReadManagedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        fake.ReadModelsYml().ShouldBe(invalid);
        read.Providers.ShouldBeEmpty();
        read.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ABrokenProviderIsRejectedBeforeAnythingIsTouched()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options());
        var literalKey = OmpModelsFixtures.Keyed() with { ApiKeyEnvName = "sk-live-aaaa" };

        var result = await file.UpsertAsync(literalKey, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Rejected);
        result.Message.ShouldNotContain("sk-live-aaaa", Case.Sensitive);
        Text(fake.ReadModelsYml()).ShouldBe(Original);
        fake.Calls().ShouldBeEmpty();
        (await file.RemoveAsync("Bad Id", TestContext.Current.CancellationToken).ConfigureAwait(true)).Status.ShouldBe(OmpModelsWriteStatus.Rejected);
    }

    [Fact]
    public async Task ReadManagedReturnsWhatWasWrittenAndNothingForAMissingFile()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());

        var missing = await file.ReadManagedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = await file.UpsertAsync(OmpModelsFixtures.Keyed(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var read = await file.ReadManagedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        missing.Providers.ShouldBeEmpty();
        missing.Problems.ShouldBeEmpty();
        read.Problems.ShouldBeEmpty();
        read.Providers.Single().Id.ShouldBe("gateway");
        read.Providers.Single().Models.ShouldBe(OmpModelsFixtures.Keyed().Models);
    }

    [Fact]
    public async Task ConcurrentUpsertsOfDifferentProvidersAreBothKept()
    {
        using var fake = new FakeOmpEnvironment();
        var file = new OmpModelsFile(fake.Options());

        var results = await Task.WhenAll(
            file.UpsertAsync(OmpModelsFixtures.Local("first"), TestContext.Current.CancellationToken),
            file.UpsertAsync(OmpModelsFixtures.Keyed("second"), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        results.ShouldAllBe(result => result.Status == OmpModelsWriteStatus.Written);
        var read = await file.ReadManagedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        read.Providers.Select(provider => provider.Id).Order(StringComparer.Ordinal).ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task MessagesNeverNameAPathOutsideTheAgentDirectory()
    {
        using var fake = new FakeOmpEnvironment();
        fake.WriteModelsYml(Original);
        var file = new OmpModelsFile(fake.Options());
        var broken = OmpModelsFixtures.Local() with { Models = [new OmpManagedModel("BADMODEL")] };

        var rejected = await file.UpsertAsync(broken, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var conflict = await file.UpsertAsync(OmpModelsFixtures.Local("mine"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        rejected.Message.ShouldNotContain(fake.Root, Case.Sensitive);
        conflict.Message.ShouldNotContain(fake.Root, Case.Sensitive);
    }

    private static string Text(byte[]? bytes) => Encoding.UTF8.GetString(bytes!);
}
