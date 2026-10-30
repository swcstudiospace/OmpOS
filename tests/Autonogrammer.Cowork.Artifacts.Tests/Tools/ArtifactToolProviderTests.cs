using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Artifacts.Tools;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Tools;

/// <summary>The artifact tool family: grants, exact edits, folder refusal and the write limiter.</summary>
public sealed class ArtifactToolProviderTests
{
    /// <summary>Verifies descriptors, effects and the create/update grant preview.</summary>
    [Fact]
    public async Task DescriptorsAndGrantPreviewMatchTheFamily()
    {
        var provider = Provider(out _);
        var tools = provider.Describe();
        tools.Select(static tool => tool.Name).ShouldBe(["artifact_create", "artifact_update", "artifact_read", "artifact_list"], ignoreOrder: false);
        tools.ShouldAllBe(static tool => string.Equals(tool.ProviderId, "artifact", StringComparison.Ordinal) && tool.Results == CoworkResultKinds.Text && tool.Approval == CoworkApprovalRequirement.ByPolicy && tool.Effect != CoworkToolEffect.Destructive);
        var preview = await provider.PreviewAsync(Session("/tmp"), Invoke("artifact_create", """{"title":"Note","kind":"Markdown"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        preview.Grant.ShouldBe(new CoworkGrantKey(ArtifactToolProvider.GrantScope, ArtifactToolProvider.GrantValue));
        var read = await provider.PreviewAsync(Session("/tmp"), Invoke("artifact_read", """{"artifact_id":"x"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        read.Grant.ShouldBeNull();
    }

    /// <summary>Verifies create, unique edits, a double match that writes nothing, and an untrusted read.</summary>
    [Fact]
    public async Task CreateUpdateAndReadStayTyped()
    {
        var provider = Provider(out var service);
        var session = Session(Path.GetTempPath());
        var created = await provider.InvokeAsync(session, Invoke("artifact_create", """{"title":"Note","kind":"Markdown","content":"alpha"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.IsError.ShouldBeFalse();
        created.Content[0].ShouldBeOfType<CoworkTextBlock>().IsUntrusted.ShouldBeFalse();
        var id = JsonDocument.Parse(((CoworkTextBlock)created.Content[0]).Text).RootElement.GetProperty("artifact_id").GetString()!;
        var refused = await provider.InvokeAsync(session, Invoke("artifact_update", "{\"artifact_id\":\"" + id + "\",\"base_version\":1,\"edits\":[{\"old\":\"nope\",\"new\":\"x\"}]}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        refused.IsError.ShouldBeTrue();
        ((CoworkTextBlock)refused.Content[0]).Text.ShouldContain("edit_not_found");
        (await service.ReadAsync(id, 1, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value!.Content.ToArray().ShouldBe("alpha"u8.ToArray());
        var updated = await provider.InvokeAsync(session, Invoke("artifact_update", "{\"artifact_id\":\"" + id + "\",\"base_version\":1,\"edits\":[{\"old\":\"alpha\",\"new\":\"beta\"}]}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        updated.IsError.ShouldBeFalse();
        var read = await provider.InvokeAsync(session, Invoke("artifact_read", "{\"artifact_id\":\"" + id + "\",\"version\":2}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var block = read.Content[0].ShouldBeOfType<CoworkTextBlock>();
        block.IsUntrusted.ShouldBeTrue();
        block.Text.ShouldContain("<untrusted>");
        block.Text.ShouldContain("beta");
    }

    /// <summary>Verifies path-escape.txt and a leaving symlink and a directory are file_not_allowed, and the 21st write is rate limited.</summary>
    [Fact]
    public async Task FromFileAndWriteLimitAreRefused()
    {
        var root = Directory.CreateTempSubdirectory("ag-art-");
        var provider = Provider(out _, 2);
        var session = Session(root.FullName);
        var outside = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "tools", "path-escape.txt"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Trim();
        outside.ShouldStartWith("/");
        var escaped = await provider.InvokeAsync(session, Invoke("artifact_create", "{\"title\":\"X\",\"kind\":\"Markdown\",\"from_file\":\"" + outside + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        ((CoworkTextBlock)escaped.Content[0]).Text.ShouldContain("file_not_allowed");
        var link = Path.Combine(root.FullName, "leave");
        var target = Path.Combine(Path.GetTempPath(), "ag-art-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "secret.txt"), "secret", TestContext.Current.CancellationToken).ConfigureAwait(true);
        File.CreateSymbolicLink(link, Path.Combine(target, "secret.txt"));
        var linked = await provider.InvokeAsync(session, Invoke("artifact_create", "{\"title\":\"X\",\"kind\":\"Markdown\",\"from_file\":\"" + link + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        ((CoworkTextBlock)linked.Content[0]).Text.ShouldContain("file_not_allowed");
        var directory = await provider.InvokeAsync(session, Invoke("artifact_create", "{\"title\":\"X\",\"kind\":\"Markdown\",\"from_file\":\"" + root.FullName + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        ((CoworkTextBlock)directory.Content[0]).Text.ShouldContain("file_not_allowed");
        var first = await provider.InvokeAsync(session, Invoke("artifact_create", """{"title":"One","kind":"Markdown","content":"a"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        first.IsError.ShouldBeFalse();
        var second = await provider.InvokeAsync(session, Invoke("artifact_create", """{"title":"Two","kind":"Markdown","content":"b"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        second.IsError.ShouldBeFalse();
        var limited = await provider.InvokeAsync(session, Invoke("artifact_create", """{"title":"Three","kind":"Markdown","content":"c"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var text = ((CoworkTextBlock)limited.Content[0]).Text;
        text.ShouldContain("rate_limited");
        text.ShouldContain("retry-after");
    }

    private static ArtifactToolProvider Provider(out ArtifactService service, int writes = 20)
    {
        service = new ArtifactService(new InMemoryArtifactStore(), TimeProvider.System, Options.Create(new ArtifactOptions()));
        return new ArtifactToolProvider(service, new ArtifactWriteLimiter(TimeProvider.System), Options.Create(new ArtifactOptions { MaxWritesPerMinutePerSession = writes }));
    }

    private static CoworkSessionContext Session(string folder) => new()
    {
        SessionId = "session-1",
        WorkingDirectory = folder,
        TrustedFolders = [folder],
        ModelReadsImages = false,
    };

    private static CoworkToolInvocation Invoke(string name, string json) =>
        new("session-1", "call-1", name, JsonDocument.Parse(json).RootElement, CoworkTurnOrigin.Local);
}
