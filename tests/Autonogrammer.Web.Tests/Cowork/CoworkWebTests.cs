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
    /// <summary>List, edit, delete, global load and save, plus offline and error fixtures.</summary>
    [Fact]
    public async Task ClientsCoverListEditDeleteAndInstructions()
    {
        var handler = new ScriptedHandler();
        using var http = new ApiHttp(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
        var projects = new CoworkProjectsClient(http);
        var memory = new CoworkMemoryClient(http);

        handler.Status = HttpStatusCode.OK;
        handler.Body = """[{"id":"project-ready","name":"Ready project"}]""";
        var listed = await projects.ListAsync(CancellationToken.None);
        listed.Succeeded.ShouldBeTrue();
        listed.Value.ShouldNotBeNull();
        listed.Value!.Count.ShouldBe(1);
        listed.Value[0].Name.ShouldBe(DemoCoworkFixtures.ReadyProject.Name);

        handler.Body = """{"id":"project-ready","name":"Renamed"}""";
        var edited = await projects.UpsertAsync(DemoCoworkFixtures.ReadyProject with { Name = "Renamed" }, CancellationToken.None);
        edited.Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Put);
        handler.LastPath.ShouldBe("/cowork/projects/project-ready");

        var deleted = await projects.DeleteAsync("project-ready", CancellationToken.None);
        deleted.Succeeded.ShouldBeTrue();
        handler.LastMethod.ShouldBe(HttpMethod.Delete);

        handler.Body = """{"instructions":"Prefer short answers."}""";
        var loaded = await projects.GetGlobalInstructionsAsync(CancellationToken.None);
        loaded.Value.ShouldBe(DemoCoworkFixtures.ReadyInstructions);

        var saved = await projects.PutGlobalInstructionsAsync(DemoCoworkFixtures.ReadyInstructions, CancellationToken.None);
        saved.Succeeded.ShouldBeTrue();
        handler.LastPath.ShouldBe("/cowork/instructions/global");

        var refused = await projects.PutGlobalInstructionsAsync(DemoCoworkFixtures.OverBudgetInstructions, CancellationToken.None);
        refused.Succeeded.ShouldBeFalse();
        refused.Sentence.ShouldBe(GlobalInstructionsModel.LimitMessage);
        handler.LastPath.ShouldBe("/cowork/instructions/global");

        handler.Status = HttpStatusCode.OK;
        handler.Body = """[{"id":"memory-ready","content":"The user prefers short answers.","sourceSession":"session-ready"}]""";
        var remembered = await memory.ListAsync(CancellationToken.None);
        remembered.Value![0].SourceSession.ShouldBe(DemoCoworkFixtures.ReadyMemory.SourceSession);
        (await memory.DeleteAsync("memory-ready", CancellationToken.None)).Succeeded.ShouldBeTrue();

        handler.Throw = true;
        var offline = await projects.ListAsync(CancellationToken.None);
        offline.Sentence.ShouldBe(DemoCoworkFixtures.OfflineSentence);

        handler.Throw = false;
        handler.Status = HttpStatusCode.NotFound;
        var disabled = await projects.ListAsync(CancellationToken.None);
        disabled.Sentence.ShouldBe(DemoCoworkFixtures.ErrorSentence);

        DemoCoworkFixtures.EmptyProjects.ShouldBeEmpty();
        DemoCoworkFixtures.EmptyMemory.ShouldBeEmpty();
        DemoCoworkFixtures.EmptyInstructions.ShouldBeEmpty();
        var wording = CoworkProjectsClient.ScheduleWording(DemoCoworkFixtures.ScheduledTask);
        wording.ShouldContain("Next run");
        wording.ShouldContain("Last run");
        wording.ShouldContain("Missed runs");
        wording.ShouldContain("desktop");
    }
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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
