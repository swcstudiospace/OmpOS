using System.Text.RegularExpressions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Secrets;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// The environment variables that carry the provider keys of OmpOS-managed providers into the omp processes the head starts: the
/// variable names, what is read from the key store, the merge after planning and the live view, and that no rendering of any of
/// them shows a value. No omp or process is involved.
/// </summary>
public sealed partial class ProviderKeyEnvironmentTests
{
    private const string Name = "OMPOS_PROVIDER_GATEWAY_KEY";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("gateway", "OMPOS_PROVIDER_GATEWAY_KEY")]
    [InlineData("a.b-c_d", "OMPOS_PROVIDER_A_B_C_D_KEY")]
    [InlineData("llama.cpp", "OMPOS_PROVIDER_LLAMA_CPP_KEY")]
    [InlineData("0day", "OMPOS_PROVIDER_0DAY_KEY")]
    public void TheVariableNameIsDeterministicAndOmpAcceptsIt(string providerId, string expected)
    {
        var name = ProviderKeyEnvironment.EnvNameFor(providerId);

        name.ShouldBe(expected);
        EnvNamePattern().IsMatch(name).ShouldBeTrue();
        ProviderKeyEnvironment.IsKeyName(name).ShouldBeTrue();
    }

    [Fact]
    public void TheLongestProviderIdStillGivesANameWithinOmpsLimit()
    {
        var name = ProviderKeyEnvironment.EnvNameFor(new string('x', 64));

        name.Length.ShouldBeLessThanOrEqualTo(128);
        EnvNamePattern().IsMatch(name).ShouldBeTrue();
    }

    [Theory]
    [InlineData("OMPOS_PROVIDER_X_KEY", true)]
    [InlineData("OMPOS_PROVIDER__KEY", false)]
    [InlineData("OMPOS_PROVIDER_x_KEY", false)]
    [InlineData("OMPOS_PROVIDER_X", false)]
    [InlineData("PATH", false)]
    [InlineData("OPENAI_API_KEY", false)]
    [InlineData("", false)]
    public void OnlyTheNamesOfProviderKeysBelongToTheEnvironment(string name, bool expected) =>
        ProviderKeyEnvironment.IsKeyName(name).ShouldBe(expected);

    [Fact]
    public async Task OnlyProviderKeyNamesAreBuiltFromTheStore()
    {
        var store = new InMemorySessionSecretStore();
        await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);
        await store.SetAsync("OPENAI_API_KEY", new SecretString("not-a-provider-key"), Token).ConfigureAwait(true);
        var keys = new ProviderKeyEnvironment(store);

        var built = await keys.BuildAsync(Token).ConfigureAwait(true);

        built.Keys.ShouldBe([Name]);
        built[Name].ShouldBe(ModelsForms.Canary);
        keys.Current.ShouldBeEmpty("building does not replace the snapshot");
    }

    [Fact]
    public async Task RefreshReplacesTheSnapshotAndAKeyRemovedFromTheStoreLeavesIt()
    {
        var store = new InMemorySessionSecretStore();
        var keys = new ProviderKeyEnvironment(store);
        await keys.RefreshAsync(Token).ConfigureAwait(true);
        keys.Current.ShouldBeEmpty();
        await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);

        await keys.RefreshAsync(Token).ConfigureAwait(true);
        keys.Current.Keys.ShouldBe([Name]);
        _ = await store.RemoveAsync(Name, Token).ConfigureAwait(true);
        await keys.RefreshAsync(Token).ConfigureAwait(true);

        keys.Current.ShouldBeEmpty();
        keys.Problem.ShouldBeNull();
    }

    [Fact]
    public async Task AStoreThatCannotBeReadKeepsThePreviousSnapshotAndReportsAPlainProblem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ompos-keyenv-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "secrets.json");
            var store = new OwnerOnlyFileSecretStore(path);
            await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);
            var keys = new ProviderKeyEnvironment(store);
            await keys.RefreshAsync(Token).ConfigureAwait(true);
            await File.WriteAllTextAsync(path, "corrupt", Token).ConfigureAwait(true);

            await keys.RefreshAsync(Token).ConfigureAwait(true);

            keys.Current.Keys.ShouldBe([Name], "a corrupt file does not take the keys of running sessions away");
            keys.Problem.ShouldNotBeNull().ShouldContain("secret file", Case.Sensitive);
            keys.Problem.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
            (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe("corrupt");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MergeReturnsACopyWhereAKeyReplacesAVariableOfTheSameName()
    {
        var store = new InMemorySessionSecretStore();
        await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);
        var keys = new ProviderKeyEnvironment(store);
        await keys.RefreshAsync(Token).ConfigureAwait(true);
        var planned = new Dictionary<string, string>(StringComparer.Ordinal) { ["PI_CODING_AGENT_DIR"] = "/state", [Name] = "planned" };

        var merged = keys.Merge(planned);

        merged["PI_CODING_AGENT_DIR"].ShouldBe("/state");
        merged[Name].ShouldBe(ModelsForms.Canary);
        planned[Name].ShouldBe("planned");
        planned.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TheOverlayReadsTheKeysWhenItIsReadSoALaterKeyIsSeen()
    {
        var store = new InMemorySessionSecretStore();
        var keys = new ProviderKeyEnvironment(store);
        var planned = new Dictionary<string, string>(StringComparer.Ordinal) { ["PI_CODING_AGENT_DIR"] = "/state" };
        var overlay = keys.Overlay(planned);
        overlay.ContainsKey(Name).ShouldBeFalse();
        overlay.Count.ShouldBe(1);

        await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);
        await keys.RefreshAsync(Token).ConfigureAwait(true);

        overlay.ContainsKey(Name).ShouldBeTrue();
        overlay.TryGetValue(Name, out var value).ShouldBeTrue();
        value.ShouldBe(ModelsForms.Canary);
        overlay["PI_CODING_AGENT_DIR"].ShouldBe("/state");
        overlay.Count.ShouldBe(2);
        overlay.Keys.Order(StringComparer.Ordinal).ShouldBe(["OMPOS_PROVIDER_GATEWAY_KEY", "PI_CODING_AGENT_DIR"]);
        overlay.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)[Name].ShouldBe(ModelsForms.Canary);
        Should.Throw<KeyNotFoundException>(() => overlay["MISSING"]);
        planned.Count.ShouldBe(1, "the plan's own environment is never changed");
    }

    [Fact]
    public async Task NoRenderingOfTheKeyEnvironmentThePlanOrTheLaunchValuesShowsAKey()
    {
        var store = new InMemorySessionSecretStore();
        await store.SetAsync(Name, new SecretString(ModelsForms.Canary), Token).ConfigureAwait(true);
        var keys = new ProviderKeyEnvironment(store);
        await keys.RefreshAsync(Token).ConfigureAwait(true);
        var planned = new Dictionary<string, string>(StringComparer.Ordinal) { ["PI_CODING_AGENT_DIR"] = "/state" };
        var overlay = keys.Overlay(planned);
        var plan = new OmpLaunchPlan
        {
            ExtraArguments = ["--no-extensions"],
            Environment = overlay,
            McpServers = [],
            WorkingDirectory = "/workspace",
            PlanningEvents = [],
        };
        var options = new AcpClientOptions { EnvironmentVariables = overlay };
        var request = new OmpTerminalLaunchRequest("/workspace", [], 80, 24, new NullSink()) { Environment = keys.Merge(planned) };
        var session = new TerminalSessionOptions("/bin/omp", [], "/workspace", 80, 24, new NullSink()) { Environment = keys.Merge(planned) };

        var renderings = new[]
        {
            keys.ToString(),
            overlay.ToString()!,
            plan.ToString(),
            plan.ForTerminal().ToString(),
            options.ToString(),
            request.ToString(),
            session.ToString(),
        };

        renderings.ShouldAllBe(text => !text.Contains(ModelsForms.Canary, StringComparison.Ordinal));
        renderings[0].ShouldContain(Name, Case.Sensitive);
        plan.ToString().ShouldContain(Name, Case.Sensitive);
        request.ToString().ShouldContain(Name, Case.Sensitive);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvNamePattern();

    private sealed class NullSink : IProcessOutputSink
    {
        public void Write(string text)
        {
        }
    }
}
