using System.Net;
using System.Text;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Autonogrammer.Web.Services.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Cowork;

/// <summary>Projects, memory and global instructions against demo fixtures. No localStorage.</summary>
public sealed class CoworkWebTests
{
    /// <summary>Projects can be listed, renamed and deleted through the project endpoints.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ProjectsCanBeListedEditedAndDeleted()
    {
        using var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);
        var ct = TestContext.Current.CancellationToken;

        handler.Body = """[{"id":"project-ready","name":"Ready project"}]""";
        var listed = await projects.ListAsync(ct).ConfigureAwait(true);
        listed.Succeeded.ShouldBeTrue();
        listed.Value.ShouldNotBeNull();
        listed.Value!.Count.ShouldBe(1);
        listed.Value[0].Name.ShouldBe("Ready project");

        handler.Body = """{"id":"project-ready","name":"Renamed"}""";
        var edited = await projects.UpsertAsync(DemoCoworkFixtures.ReadyProject with { Name = "Renamed" }, ct).ConfigureAwait(true);
        edited.Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Put);
        handler.LastPath.ShouldBe("/cowork/projects/project-ready");

        var deleted = await projects.DeleteAsync("project-ready", ct).ConfigureAwait(true);
        deleted.Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Delete);
        handler.LastPath.ShouldBe("/cowork/projects/project-ready");
    }

    /// <summary>Global instructions are read and saved through the global instructions endpoint.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task GlobalInstructionsCanBeLoadedAndSaved()
    {
        using var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);
        var ct = TestContext.Current.CancellationToken;

        handler.Body = """{"instructions":"Prefer short answers."}""";
        var loaded = await projects.GetGlobalInstructionsAsync(ct).ConfigureAwait(true);
        loaded.Succeeded.ShouldBeTrue();
        loaded.Value.ShouldBe("Prefer short answers.");

        var saved = await projects.PutGlobalInstructionsAsync(DemoCoworkFixtures.ReadyInstructions, ct).ConfigureAwait(true);
        saved.Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Put);
        handler.LastPath.ShouldBe("/cowork/instructions/global");
    }

    /// <summary>Over-budget global instructions are refused before any HTTP request is sent.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task OverBudgetInstructionsAreRefusedWithoutSendingARequest()
    {
        using var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);

        var refused = await projects.PutGlobalInstructionsAsync(
            DemoCoworkFixtures.OverBudgetInstructions,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        refused.Succeeded.ShouldBeFalse();
        refused.Sentence.ShouldBe(GlobalInstructionsModel.LimitMessage);
        handler.RequestCount.ShouldBe(0);
    }

    /// <summary>Memory preserves the source session when listed, and entries can be deleted.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task MemoryCanBeListedAndDeleted()
    {
        using var handler = new ScriptedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var memory = new CoworkMemoryClient(http);
        var ct = TestContext.Current.CancellationToken;

        handler.Body = """[{"id":"memory-ready","content":"The user prefers short answers.","sourceSession":"session-ready"}]""";
        var remembered = await memory.ListAsync(ct).ConfigureAwait(true);
        remembered.Succeeded.ShouldBeTrue();
        remembered.Value.ShouldNotBeNull();
        remembered.Value!.Count.ShouldBe(1);
        remembered.Value[0].SourceSession.ShouldBe("session-ready");

        (await memory.DeleteAsync("memory-ready", ct).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Delete);
        handler.LastPath.ShouldBe("/cowork/memory/memory-ready");
    }

    /// <summary>An unreachable project API returns the offline result instead of throwing.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnreachableProjectApiReturnsTheOfflineResult()
    {
        using var handler = new ScriptedHandler { Throw = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);

        var offline = await projects.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        offline.Succeeded.ShouldBeFalse();
        offline.Value.ShouldBeNull();
        offline.Sentence.ShouldBe(DemoCoworkFixtures.OfflineSentence);
    }

    /// <summary>A missing project API returns the disabled-API result.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AMissingProjectApiReturnsTheDisabledResult()
    {
        using var handler = new ScriptedHandler { Status = HttpStatusCode.NotFound };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);

        var disabled = await projects.ListAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        disabled.Succeeded.ShouldBeFalse();
        disabled.Value.ShouldBeNull();
        disabled.Sentence.ShouldBe(DemoCoworkFixtures.ErrorSentence);
    }

    /// <summary>Empty project, memory and instruction responses are successful empty results.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EmptyResponsesAreSuccessfulEmptyResults()
    {
        using var handler = new ScriptedHandler { Body = "[]" };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        using var http = new ApiHttp(client);
        var projects = new CoworkProjectsClient(http);
        var memory = new CoworkMemoryClient(http);
        var ct = TestContext.Current.CancellationToken;

        var listed = await projects.ListAsync(ct).ConfigureAwait(true);
        listed.Succeeded.ShouldBeTrue();
        listed.Value.ShouldNotBeNull();
        listed.Value!.ShouldBeEmpty();

        var remembered = await memory.ListAsync(ct).ConfigureAwait(true);
        remembered.Succeeded.ShouldBeTrue();
        remembered.Value.ShouldNotBeNull();
        remembered.Value!.ShouldBeEmpty();

        handler.Body = """{"instructions":""}""";
        var loaded = await projects.GetGlobalInstructionsAsync(ct).ConfigureAwait(true);
        loaded.Succeeded.ShouldBeTrue();
        loaded.Value.ShouldBeEmpty();
        DemoCoworkFixtures.EmptyProjects.ShouldBeEmpty();
        DemoCoworkFixtures.EmptyMemory.ShouldBeEmpty();
        DemoCoworkFixtures.EmptyInstructions.ShouldBeEmpty();
    }

    /// <summary>The web list wording names the next run, the last run, the missed runs and the desktop-only sentence.</summary>
    [Fact]
    public void ScheduleWordingNamesTheRunFactsAndTheDesktopOnlySentence()
    {
        var wording = CoworkProjectsClient.ScheduleWording(DemoCoworkFixtures.ScheduledTask);
        wording.ShouldContain("Next run");
        wording.ShouldContain("Last run");
        wording.ShouldContain("Missed runs");
        wording.ShouldContain("desktop");
    }

    /// <summary>The web Cowork client, fixtures and views use no localStorage and no desktop Cowork project reference.</summary>
    [Fact]
    public void NewWebCodeDoesNotUseLocalStorageOrCowork()
    {
        var root = RepoRoot();
        var files = new[]
        {
            "apps/Autonogrammer.Web/Services/Cowork/CoworkProjectsClient.cs",
            "apps/Autonogrammer.Web/Services/Cowork/CoworkMemoryClient.cs",
            "apps/Autonogrammer.Web/Demo/DemoCoworkFixtures.cs",
            "src/Autonogrammer.UI/Components/Cowork/ProjectsView.razor",
            "src/Autonogrammer.UI/Components/Cowork/MemoryView.razor",
            "src/Autonogrammer.UI/Components/Cowork/InstructionsView.razor",
        };
        foreach (var relative in files)
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            text.ShouldNotContain("localStorage");
            text.ShouldNotContain("Autonogrammer.Cowork");
        }

        File.ReadAllText(Path.Combine(root, "apps/Autonogrammer.Web/Autonogrammer.Web.csproj")).ShouldNotContain("Autonogrammer.Cowork.csproj");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "apps/Autonogrammer.Web/Autonogrammer.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }



    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string Body { get; set; } = "{}";

        public bool Throw { get; set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastPath { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastMethod = request.Method;
            LastPath = request.RequestUri?.AbsolutePath;
            if (Throw)
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
