using System.Text.Json;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Behaviour tests for the <see cref="SandboxProviderOptions.PerSandboxCredentials"/> and
/// <see cref="SandboxProviderOptions.AllowHostSecrets"/> flags: each flag changes what the podman provider runs,
/// writes into the compose file, and records on the sandbox.
/// </summary>
public sealed class PerSandboxCredentialFlagTests
{
    private const string ContainerId = "abcdef123456";

    /// <summary>
    /// With default options the provider issues one secret create whose argv carries the secret name and a staged path only.
    /// </summary>
    [Fact]
    public async Task DefaultOptions_IssueSecretCreateWithNameAndStagedPathOnly()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions());
        var ct = TestContext.Current.CancellationToken;

        var sandboxId = (await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct)).Descriptor.Id;

        var create = runner.Specs.Single(IsSecretCreate);
        create.Argv.Count.ShouldBe(5);
        create.Argv[0].ShouldBe("podman");
        create.Argv[1].ShouldBe("secret");
        create.Argv[2].ShouldBe("create");
        create.Argv[3].ShouldBe($"autonogrammer-{sandboxId}-credential");
        Path.GetFileName(create.Argv[4]).ShouldStartWith($"autonogrammer-{sandboxId}-credential-");

        await provider.TeardownAsync(sandboxId, ct);
    }

    /// <summary>
    /// With PerSandboxCredentials=false no podman secret command runs and the compose text carries no secret declaration or mount.
    /// </summary>
    [Fact]
    public async Task PerSandboxCredentialsOff_RunsNoSecretCommandAndWritesNoSecretsToCompose()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions { PerSandboxCredentials = false });
        var ct = TestContext.Current.CancellationToken;

        var result = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);
        var sandboxId = result.Descriptor.Id;

        runner.Specs.Any(IsPodmanSecret).ShouldBeFalse();
        runner.Specs.Any(static s => PodmanTestData.HasArgument(s, "up")).ShouldBeTrue();

        var composeText = await File.ReadAllTextAsync(ComposeFileOf(runner), ct);
        composeText.Contains("secret", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        using (var document = JsonDocument.Parse(composeText))
        {
            var root = document.RootElement;
            root.TryGetProperty("secrets", out _).ShouldBeFalse();
            root.GetProperty("services").GetProperty("sandbox").TryGetProperty("secrets", out _).ShouldBeFalse();
        }

        result.Descriptor.Labels.Keys.Concat(result.Descriptor.Labels.Values)
            .Any(static s => s.Contains("credential", StringComparison.OrdinalIgnoreCase) || s.Contains("secret", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse();

        await provider.TeardownAsync(sandboxId, ct);
        runner.Specs.Any(IsPodmanSecret).ShouldBeFalse();
        runner.Specs.Any(static s => PodmanTestData.HasArgument(s, "down")).ShouldBeTrue();
    }

    /// <summary>
    /// With PerSandboxCredentials=false a failed compose up runs the partial down but no secret removal.
    /// </summary>
    [Fact]
    public async Task PerSandboxCredentialsOff_FailedUp_RunsNoSecretRemoval()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false });
        runner.SetResult(PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions { PerSandboxCredentials = false });
        var ct = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        runner.Specs.Any(IsPodmanSecret).ShouldBeFalse();
        runner.Specs.Any(static s => PodmanTestData.HasArgument(s, "down")).ShouldBeTrue();
    }

    /// <summary>
    /// A host secret reference is rejected with the host-secrets policy, before any process runs, whatever PerSandboxCredentials is set to.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HostSecretReference_WithHostSecretsDisallowed_ThrowsPolicyDeniedWithZeroRunnerCalls(bool perSandboxCredentials)
    {
        var runner = new FakeProcessRunner();
        var options = new SandboxProviderOptions
        {
            AllowHostSecrets = false,
            PerSandboxCredentials = perSandboxCredentials,
        };
        var provider = PodmanTestData.CreateProvider(runner, options: options);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest() with
        {
            SecretRefs = [new SecretRef { Key = "host-token", ProviderSecretId = "host-secret-id", TargetEnvVar = "API_TOKEN" }],
        };

        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request, ct));

        exception.Policy.ShouldBe("host-secrets");
        exception.Message.ShouldContain("AllowHostSecrets");
        runner.Specs.ShouldBeEmpty();
    }

    /// <summary>
    /// With AllowHostSecrets=true the local podman provider cannot deliver secret references, so a non-empty SecretRefs
    /// throws NotAvailableException before any process runs (TD-19).
    /// </summary>
    [Fact]
    public async Task HostSecretReference_WithHostSecretsAllowed_ThrowsNotAvailableWithZeroRunnerCalls()
    {
        var runner = new FakeProcessRunner();
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions { AllowHostSecrets = true });
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest() with
        {
            SecretRefs = [new SecretRef { Key = "host-token", ProviderSecretId = "host-secret-id", TargetEnvVar = "API_TOKEN" }],
        };

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request, ct));

        runner.Specs.ShouldBeEmpty();
    }

    private static bool IsSecretCreate(ProcessSpec spec) => spec.Argv.Count == 5 && IsSecretVerb(spec, "create");

    private static bool IsPodmanSecret(ProcessSpec spec) =>
        spec.Argv.Count > 1
        && string.Equals(spec.Argv[0], "podman", StringComparison.Ordinal)
        && string.Equals(spec.Argv[1], "secret", StringComparison.Ordinal);

    private static bool IsSecretVerb(ProcessSpec spec, string verb) =>
        spec.Argv.Count > 2 && IsPodmanSecret(spec) && string.Equals(spec.Argv[2], verb, StringComparison.Ordinal);

    private static string ComposeFileOf(FakeProcessRunner runner) =>
        runner.Specs.First(static s => PodmanTestData.HasArgument(s, "up"))
            .Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
}
