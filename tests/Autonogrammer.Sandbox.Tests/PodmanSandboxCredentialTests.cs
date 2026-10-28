using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Behaviour tests for the per-sandbox podman secret credential: generation and staging, compose wiring,
/// removal at teardown and cleanup after a failed provision.
/// </summary>
public sealed class PodmanSandboxCredentialTests
{
    private const string ContainerId = "abcdef123456";
    private const string MountTarget = "autonogrammer_sandbox_credential";

    /// <summary>
    /// The create call carries the secret name and a staging path only; the staged file exists during create,
    /// holds 32 decoded bytes with owner read-write permissions, and is deleted afterwards.
    /// </summary>
    [Fact]
    public async Task Create_ArgvCarriesNameAndPathOnly_AndStagedFileHoldsThirtyTwoBytes()
    {
        var runner = new FakeProcessRunner();
        var staged = StagedCredential.Capture(runner);
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var sandboxId = (await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct)).Descriptor.Id;
        var secretName = $"autonogrammer-{sandboxId}-credential";

        var create = runner.Specs.Single(IsSecretCreate);
        create.Argv.ShouldBe(["podman", "secret", "create", secretName, staged.Path!]);
        staged.ExistedWhenCreated.ShouldBeTrue();
        staged.Value.ShouldNotBeNull();
        staged.Value!.Length.ShouldBe(43);
        Base64Url.DecodeFromChars(staged.Value!.AsSpan()).Length.ShouldBe(32);
        if (!OperatingSystem.IsWindows())
        {
            staged.UnixMode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Exists(staged.Path!).ShouldBeFalse();

        await provider.TeardownAsync(sandboxId, ct);
    }

    /// <summary>
    /// The credential value never appears in any argv, environment, the compose text, descriptor labels or logs.
    /// </summary>
    [Fact]
    public async Task CredentialValue_NeverAppearsInArgvComposeTextLabelsOrLogs()
    {
        var runner = new FakeProcessRunner();
        var staged = StagedCredential.Capture(runner);
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var logger = new CapturingLogger<LocalPodmanComposeProvider>();
        var provider = PodmanTestData.CreateProvider(runner, logger);
        var ct = TestContext.Current.CancellationToken;

        var result = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);
        staged.Value.ShouldNotBeNull();
        var value = staged.Value!;
        var composeText = await File.ReadAllTextAsync(ComposeFileOf(runner), ct);

        runner.Specs.SelectMany(static s => s.Argv).Any(a => a.Contains(value, StringComparison.Ordinal)).ShouldBeFalse();
        runner.Specs.SelectMany(static s => s.Environment.Values).Any(v => v.Contains(value, StringComparison.Ordinal)).ShouldBeFalse();
        composeText.Contains(value, StringComparison.Ordinal).ShouldBeFalse();
        result.Descriptor.Labels.Values.Any(v => v.Contains(value, StringComparison.Ordinal)).ShouldBeFalse();
        logger.Entries.Any(e => e.Message.Contains(value, StringComparison.Ordinal)).ShouldBeFalse();

        await provider.TeardownAsync(result.Descriptor.Id, ct);
    }

    /// <summary>
    /// The compose text declares the secret as external and mounts it into the sandbox service at the credential path with mode 0400.
    /// </summary>
    [Fact]
    public async Task ComposeText_MountsExternalSecretAtSandboxCredentialPath()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var sandboxId = (await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct)).Descriptor.Id;
        var secretName = $"autonogrammer-{sandboxId}-credential";

        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(ComposeFileOf(runner), ct)))
        {
            var root = document.RootElement;
            var mount = root.GetProperty("services").GetProperty("sandbox").GetProperty("secrets")[0];
            mount.GetProperty("source").GetString().ShouldBe(secretName);
            mount.GetProperty("target").GetString().ShouldBe(MountTarget);
            mount.GetProperty("mode").GetString().ShouldBe("0400");
            root.GetProperty("secrets").GetProperty(secretName).GetProperty("external").GetBoolean().ShouldBeTrue();
        }

        await provider.TeardownAsync(sandboxId, ct);
    }

    /// <summary>
    /// Teardown removes the secret after compose down has run, with argv carrying only the secret name.
    /// </summary>
    [Fact]
    public async Task Teardown_RemovesSecretAfterComposeDown()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var sandboxId = (await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct)).Descriptor.Id;
        var secretName = $"autonogrammer-{sandboxId}-credential";

        await provider.TeardownAsync(sandboxId, ct);

        var specs = runner.Specs.ToList();
        var downIndex = specs.FindIndex(static s => PodmanTestData.HasArgument(s, "down"));
        var rmIndex = specs.FindIndex(IsSecretRemove);
        downIndex.ShouldBeGreaterThanOrEqualTo(0);
        rmIndex.ShouldBeGreaterThan(downIndex);
        specs[rmIndex].Argv.ShouldBe(["podman", "secret", "rm", secretName]);
    }

    /// <summary>
    /// A non-zero secret removal at teardown throws the typed teardown failure and keeps the record, so a retry re-runs down and removal.
    /// </summary>
    [Fact]
    public async Task Teardown_FailedSecretRemoval_ThrowsTypedAndRetainsRecordForRetry()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            PodmanTestData.Ok(),
            PodmanTestData.Ok(ContainerId),
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "secret in use", TimedOut = false });
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var sandboxId = (await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct)).Descriptor.Id;

        var ex = await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync(sandboxId, ct));
        ex.SandboxId.ShouldBe(sandboxId);
        ex.Message.ShouldContain(sandboxId);

        await provider.TeardownAsync(sandboxId, ct);
        runner.Specs.Count(IsSecretRemove).ShouldBe(2);
        runner.Specs.Count(static s => PodmanTestData.HasArgument(s, "down")).ShouldBe(2);
    }

    /// <summary>
    /// A failed compose up removes the secret after the partial compose down, and deletes the compose file.
    /// </summary>
    [Fact]
    public async Task FailedUp_RemovesSecretAfterPartialDown()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false });
        runner.SetResult(PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        var specs = runner.Specs.ToList();
        var secretName = runner.Specs.Single(IsSecretCreate).Argv[3];
        var upIndex = specs.FindIndex(static s => PodmanTestData.HasArgument(s, "up"));
        var downIndex = specs.FindIndex(static s => PodmanTestData.HasArgument(s, "down"));
        var rmIndex = specs.FindIndex(IsSecretRemove);
        upIndex.ShouldBeGreaterThanOrEqualTo(0);
        downIndex.ShouldBeGreaterThan(upIndex);
        rmIndex.ShouldBeGreaterThan(downIndex);
        specs[rmIndex].Argv.ShouldBe(["podman", "secret", "rm", secretName]);

        var composeFile = specs[upIndex].Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        File.Exists(composeFile).ShouldBeFalse();
    }

    /// <summary>
    /// A failed secret create throws the typed provision failure without running compose up or removing a secret that was never created,
    /// deletes the staged file, and never reports the credential value.
    /// </summary>
    [Fact]
    public async Task FailedSecretCreate_ThrowsTypedWithoutUpOrRemovalAndDeletesStagedFile()
    {
        var runner = new FakeProcessRunner();
        var staged = StagedCredential.Capture(runner);
        runner.EnqueueResults(new ProcessResult { ExitCode = 125, Stdout = string.Empty, Stderr = "secret already exists", TimedOut = false });
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var ex = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        runner.Specs.Any(static s => PodmanTestData.HasArgument(s, "up")).ShouldBeFalse();
        runner.Specs.Any(IsSecretRemove).ShouldBeFalse();
        staged.Path.ShouldNotBeNull();
        File.Exists(staged.Path!).ShouldBeFalse();
        staged.Value.ShouldNotBeNull();
        ex.Message.Contains(staged.Value!, StringComparison.Ordinal).ShouldBeFalse();
    }

    private static bool IsSecretCreate(ProcessSpec spec) => spec.Argv.Count == 5 && IsSecretVerb(spec, "create");

    private static bool IsSecretRemove(ProcessSpec spec) => spec.Argv.Count == 4 && IsSecretVerb(spec, "rm");

    private static bool IsSecretVerb(ProcessSpec spec, string verb) =>
        string.Equals(spec.Argv[0], "podman", StringComparison.Ordinal)
        && string.Equals(spec.Argv[1], "secret", StringComparison.Ordinal)
        && string.Equals(spec.Argv[2], verb, StringComparison.Ordinal);

    private static string ComposeFileOf(FakeProcessRunner runner) =>
        runner.Specs.First(static s => PodmanTestData.HasArgument(s, "up"))
            .Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));

    /// <summary>
    /// Records the staged credential file as the fake runner handles the secret create call.
    /// </summary>
    private sealed class StagedCredential
    {
        public string? Path { get; private set; }

        public string? Value { get; private set; }

        public bool ExistedWhenCreated { get; private set; }

        public UnixFileMode? UnixMode { get; private set; }

        public static StagedCredential Capture(FakeProcessRunner runner)
        {
            var staged = new StagedCredential();
            runner.PauseFor = async spec =>
            {
                if (IsSecretCreate(spec))
                {
                    staged.Path = spec.Argv[4];
                    staged.ExistedWhenCreated = File.Exists(staged.Path);
                    staged.Value = await File.ReadAllTextAsync(staged.Path, Encoding.ASCII, TestContext.Current.CancellationToken).ConfigureAwait(false);
                    staged.UnixMode = OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(staged.Path);
                }
            };
            return staged;
        }
    }
}
