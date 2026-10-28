using OmpOS.Api.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>The small rules that guard what reaches a URL, a command line or a worker's environment: repository URLs, branch names, secret names and values, and the worker address.</summary>
public sealed class CloudInputRulesTests
{
    private static readonly string[] Hosts = ["github.com"];

    /// <summary>Only https URLs of exactly owner and repository on an allowed host are accepted, and they are normalized.</summary>
    [Theory]
    [InlineData("https://github.com/acme/widgets", "acme", "widgets")]
    [InlineData("https://GitHub.com/acme/widgets.git", "acme", "widgets")]
    [InlineData("https://github.com/acme/widgets/", "acme", "widgets")]
    [InlineData("https://github.com/a.b-c/d_e.f", "a.b-c", "d_e.f")]
    public void RepositoryUrl_AcceptsOwnerAndRepository(string address, string owner, string name)
    {
        GitRepositoryUrl.TryParse(address, Hosts, out var repository, out var problem).ShouldBeTrue();

        problem.ShouldBeNull();
        var parsed = repository.ShouldNotBeNull();
        parsed.Owner.ShouldBe(owner);
        parsed.Name.ShouldBe(name);
        parsed.CloneUrl.ShouldBe($"https://github.com/{owner}/{name}");
        parsed.FullName.ShouldBe($"{owner}/{name}");
    }

    /// <summary>Credentials, ports, queries, fragments, other schemes, other hosts and odd paths are refused, and the problem never repeats the URL.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://github.com/acme/widgets")]
    [InlineData("git@github.com:acme/widgets.git")]
    [InlineData("ssh://github.com/acme/widgets")]
    [InlineData("https://user:pass@github.com/acme/widgets")]
    [InlineData("https://github.com:8443/acme/widgets")]
    [InlineData("https://github.com/acme/widgets?x=1")]
    [InlineData("https://github.com/acme/widgets#frag")]
    [InlineData("https://gitlab.com/acme/widgets")]
    [InlineData("https://github.com/acme")]
    [InlineData("https://github.com/acme/widgets/tree/main")]
    [InlineData("https://github.com/../widgets")]
    [InlineData("https://github.com/ac me/widgets")]
    public void RepositoryUrl_RefusesAnythingElse(string address)
    {
        GitRepositoryUrl.TryParse(address, Hosts, out var repository, out var problem).ShouldBeFalse();

        repository.ShouldBeNull();
        problem.ShouldNotBeNull().ShouldNotContain("pass", Case.Sensitive);
    }

    /// <summary>Branch names are limited to characters that are safe in a URL, a command and a variable, and to shapes git accepts.</summary>
    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/add-readme", true)]
    [InlineData("ompos/cloud/0123456789abcdef", true)]
    [InlineData("release-1.2.3", true)]
    [InlineData("", false)]
    [InlineData("-leading-dash", false)]
    [InlineData("/leading-slash", false)]
    [InlineData("trailing/", false)]
    [InlineData("trailing.", false)]
    [InlineData("double//slash", false)]
    [InlineData("dots..dots", false)]
    [InlineData("branch.lock", false)]
    [InlineData("part/.hidden", false)]
    [InlineData("has space", false)]
    [InlineData("semi;colon", false)]
    [InlineData("back`tick", false)]
    [InlineData("dollar$HOME", false)]
    [InlineData("new\nline", false)]
    [InlineData("branch\n", false)]
    public void BranchNames_AreRestricted(string name, bool valid)
    {
        GitBranchNames.IsValid(name).ShouldBe(valid);
    }

    /// <summary>A cloud agent's branch is derived from its id under the OmpOS prefix.</summary>
    [Fact]
    public void AgentBranch_UsesThePrefix()
    {
        GitBranchNames.ForAgent("abc").ShouldBe("ompos/cloud/abc");
        GitBranchNames.IsValid(GitBranchNames.ForAgent(Guid.NewGuid().ToString("N"))).ShouldBeTrue();
    }

    /// <summary>Secret names become environment variable names: reserved prefixes and names that steer the loader, the shell or the worker are refused.</summary>
    [Theory]
    [InlineData("API_KEY", true)]
    [InlineData("_private", true)]
    [InlineData("a", true)]
    [InlineData("OMPOS_ANYTHING", false)]
    [InlineData("ompos_lower", false)]
    [InlineData("WORKER_X", false)]
    [InlineData("Worker__Auth__Issuer", false)]
    [InlineData("RAILWAY_API_TOKEN", false)]
    [InlineData("LD_LIBRARY_PATH", false)]
    [InlineData("DYLD_INSERT_LIBRARIES", false)]
    [InlineData("NODE_OPTIONS", false)]
    [InlineData("BUN_OPTIONS", false)]
    [InlineData("DOTNET_STARTUP_HOOKS", false)]
    [InlineData("ASPNETCORE_URLS", false)]
    [InlineData("GIT_SSH_COMMAND", false)]
    [InlineData("HOME", false)]
    [InlineData("path", false)]
    [InlineData("PORT", false)]
    [InlineData("BASH_ENV", false)]
    [InlineData("OMP_AUTH_BROKER_TOKEN", false)]
    [InlineData("", false)]
    [InlineData("1ABC", false)]
    [InlineData("HAS-DASH", false)]
    [InlineData("HAS SPACE", false)]
    [InlineData("TRAILING_NEWLINE\n", false)]
    public void SecretNames_AreRestricted(string name, bool valid)
    {
        CloudSecretNames.IsValid(name).ShouldBe(valid);
    }

    /// <summary>A name longer than sixty-four characters is refused.</summary>
    [Fact]
    public void SecretNames_HaveALengthLimit()
    {
        CloudSecretNames.IsValid(new string('A', 64)).ShouldBeTrue();
        CloudSecretNames.IsValid(new string('A', 65)).ShouldBeFalse();
    }

    /// <summary>Secret values are four to 8192 characters without control characters other than line breaks and tabs.</summary>
    [Theory]
    [InlineData("abcd", true)]
    [InlineData("abc", false)]
    [InlineData("line1\nline2\ttab\r", true)]
    [InlineData("bell\u0007char", false)]
    [InlineData("nul\u0000char", false)]
    public void SecretValues_AreRestricted(string value, bool valid)
    {
        CloudSecretNames.IsValidValue(value).ShouldBe(valid);
    }

    /// <summary>A value over the length limit is refused.</summary>
    [Fact]
    public void SecretValues_HaveALengthLimit()
    {
        CloudSecretNames.IsValidValue(new string('x', CloudSecretNames.MaximumValueLength)).ShouldBeTrue();
        CloudSecretNames.IsValidValue(new string('x', CloudSecretNames.MaximumValueLength + 1)).ShouldBeFalse();
        CloudSecretNames.IsValidValue(null).ShouldBeFalse();
    }

    /// <summary>The worker address becomes a WebSocket endpoint on /acp with the scheme mapped; a credential, a query or another scheme is refused.</summary>
    [Theory]
    [InlineData("http://agent.railway.internal:8080/", "ws://agent.railway.internal:8080/acp")]
    [InlineData("https://agent.example.com/", "wss://agent.example.com/acp")]
    [InlineData("http://10.0.0.5/some/path", "ws://10.0.0.5/acp")]
    public void WorkerEndpoint_MapsTheScheme(string baseAddress, string expected)
    {
        var container = new OmpOS.Sandbox.CloudContainer("c", "s", OmpOS.Sandbox.CloudContainerPhase.Running, 1) { WorkerBaseUri = new Uri(baseAddress) };

        new WorkerEndpoints().Resolve(container).ToString().ShouldBe(expected);
    }

    /// <summary>A worker base address that is missing or unsafe is refused.</summary>
    [Theory]
    [InlineData("ftp://agent/")]
    [InlineData("http://user:pw@agent/")]
    [InlineData("http://agent/?x=1")]
    public void WorkerEndpoint_RefusesUnsafeAddresses(string baseAddress)
    {
        var container = new OmpOS.Sandbox.CloudContainer("c", "s", OmpOS.Sandbox.CloudContainerPhase.Running, 1) { WorkerBaseUri = new Uri(baseAddress) };

        Should.Throw<InvalidOperationException>(() => new WorkerEndpoints().Resolve(container));
        Should.Throw<InvalidOperationException>(() => new WorkerEndpoints().Resolve(new OmpOS.Sandbox.CloudContainer("c", "s", OmpOS.Sandbox.CloudContainerPhase.Running, 1)));
    }

    /// <summary>The public worker reference is an opaque hash: stable, not the provider id, and not reversible by inspection.</summary>
    [Fact]
    public void WorkerRef_IsOpaqueAndStable()
    {
        var reference = CloudAgentMapper.OpaqueRef("ctr-1");

        reference.ShouldStartWith("wrk_", Case.Sensitive);
        reference.Length.ShouldBe(20);
        reference.ShouldBe(CloudAgentMapper.OpaqueRef("ctr-1"));
        string.Equals(reference, CloudAgentMapper.OpaqueRef("ctr-2"), StringComparison.Ordinal).ShouldBeFalse("two containers must not share a reference");
        reference.ShouldNotContain("ctr-1", Case.Sensitive);
    }

    /// <summary>The label guard allows a removal only for the agent the label names, and an orphan only when the label names no known agent.</summary>
    [Fact]
    public void WorkerGuard_RequiresTheLabel()
    {
        var labelled = new CloudWorkerInfo("c1", "s1", null, new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = "agent-1" });
        var unlabelled = new CloudWorkerInfo("c2", "s2", null, new Dictionary<string, string>(StringComparer.Ordinal));
        var blank = new CloudWorkerInfo("c3", "s3", null, new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = " " });
        var known = new HashSet<string>(StringComparer.Ordinal) { "agent-1" };

        CloudWorkerGuard.BelongsTo(labelled, "agent-1").ShouldBeTrue();
        CloudWorkerGuard.BelongsTo(labelled, "agent-2").ShouldBeFalse();
        CloudWorkerGuard.BelongsTo(unlabelled, "agent-1").ShouldBeFalse();
        CloudWorkerGuard.IsRemovableOrphan(labelled, known).ShouldBeFalse();
        CloudWorkerGuard.IsRemovableOrphan(labelled, new HashSet<string>(StringComparer.Ordinal)).ShouldBeTrue();
        CloudWorkerGuard.IsRemovableOrphan(unlabelled, new HashSet<string>(StringComparer.Ordinal)).ShouldBeFalse();
        CloudWorkerGuard.IsRemovableOrphan(blank, new HashSet<string>(StringComparer.Ordinal)).ShouldBeFalse();
    }

    /// <summary>Repositories that share a name get distinct directories on the worker.</summary>
    [Fact]
    public void WorkerLayout_GivesCollidingNamesDistinctDirectories()
    {
        var repositories = new[]
        {
            new OmpOS.Contracts.Cloud.CloudRepository { Address = "https://github.com/acme/widgets" },
            new OmpOS.Contracts.Cloud.CloudRepository { Address = "https://github.com/other/widgets", Branch = "dev" },
            new OmpOS.Contracts.Cloud.CloudRepository { Address = "https://github.com/acme/gadgets" },
        };

        var assigned = CloudWorkerLayout.Assign(repositories, Hosts);

        assigned.Select(static item => item.Directory).ToArray().ShouldBe(["acme-widgets", "other-widgets", "gadgets"]);
        assigned[1].Branch.ShouldBe("dev");
    }
}
