using System.Text;
using OmpOS.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies both automation stores through the same checks (definitions, cursors, bounded history), the file store's round trip and its
/// fail-closed behaviour on a damaged file, and the secret stores, including that no secret reaches the automation file.
/// </summary>
public sealed class AutomationStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AutomationRunEntry Run(string id, int minute, AutomationRunStatus status = AutomationRunStatus.Succeeded) =>
        new()
        {
            RunId = id,
            Source = AutomationRunSource.Webhook,
            Summary = "push to main by octocat",
            Status = status,
            StartedAt = Kit.Start.AddMinutes(minute),
            FinishedAt = status == AutomationRunStatus.Running ? null : Kit.Start.AddMinutes(minute).AddSeconds(5),
            Error = status == AutomationRunStatus.Failed ? "boom" : null,
            ExternalId = "agent-1",
        };

    private static UserAutomation Complete() =>
        TestAutomations.Make(
            [
                TestAutomations.Cron("0 9 * * mon-fri"),
                TestAutomations.GitHubHook(events: ["push", "pull_request"], actions: ["opened", "merged"], branches: ["main", "release/*"]),
                TestAutomations.GenericHook(),
            ],
            timeZone: "UTC") with
        {
            Target = new AutomationTargetSpec
            {
                Kind = AutomationTargetKind.Cloud,
                Repositories = [new AutomationRepository("https://github.com/acme/widgets", "main", "develop"), new AutomationRepository("https://github.com/acme/gadgets")],
                SecretRefs = ["NPM_TOKEN", "DEPLOY_KEY"],
                EnvironmentId = "node-22",
                Model = "claude-sonnet",
                Mode = "agent",
            },
            AllowedTools = [AutomationTools.PullRequestCreate, AutomationTools.PullRequestComment],
            MaxConcurrentRuns = 2,
            MaxRunsPerHour = 7,
            MaxRuntimeMinutes = 45,
            UpdatedAt = Kit.Start.AddHours(1),
        };

    public static TheoryData<string> StoreKinds => ["memory", "file"];

    private static (IAutomationStore Store, IDisposable? Owner) Open(string kind, string directory) =>
        string.Equals(kind, "memory", StringComparison.Ordinal)
            ? (new InMemoryAutomationStore(), null)
            : OpenFile(Path.Combine(directory, "automations.json"));

    private static (IAutomationStore Store, IDisposable? Owner) OpenFile(string path)
    {
        var store = new FileAutomationStore(path);
        return (store, store);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task A_definition_round_trips_with_every_field(string kind)
    {
        using var directory = new TempDirectory();
        var (store, owner) = Open(kind, directory.Path);
        using var owned = owner;
        var automation = Complete();

        await store.SaveAsync(automation, Token).ConfigureAwait(true);
        var read = await store.GetAsync(automation.Id, Token).ConfigureAwait(true);

        read.ShouldNotBeNull();
        read.Id.ShouldBe(automation.Id, Case.Sensitive);
        read.Owner.ShouldBe(automation.Owner, Case.Sensitive);
        read.Name.ShouldBe(automation.Name, Case.Sensitive);
        read.Prompt.ShouldBe(automation.Prompt, Case.Sensitive);
        read.Enabled.ShouldBe(automation.Enabled);
        read.TimeZone.ShouldBe(automation.TimeZone, Case.Sensitive);
        read.Triggers.Count.ShouldBe(3);
        read.Triggers[0].Kind.ShouldBe(AutomationTriggerKind.Cron);
        read.Triggers[0].Cron.ShouldBe("0 9 * * mon-fri", Case.Sensitive);
        read.Triggers[1].Flavour.ShouldBe(WebhookFlavour.GitHub);
        read.Triggers[1].Events.ShouldBe(["push", "pull_request"]);
        read.Triggers[1].Actions.ShouldBe(["opened", "merged"]);
        read.Triggers[1].Branches.ShouldBe(["main", "release/*"]);
        read.Triggers[2].Flavour.ShouldBe(WebhookFlavour.Generic);
        read.Target.Kind.ShouldBe(AutomationTargetKind.Cloud);
        read.Target.Repositories.Count.ShouldBe(2);
        read.Target.Repositories[0].ShouldBe(new AutomationRepository("https://github.com/acme/widgets", "main", "develop"));
        read.Target.Repositories[1].ShouldBe(new AutomationRepository("https://github.com/acme/gadgets"));
        read.Target.SecretRefs.ShouldBe(["NPM_TOKEN", "DEPLOY_KEY"]);
        read.Target.EnvironmentId.ShouldBe("node-22", Case.Sensitive);
        read.Target.Model.ShouldBe("claude-sonnet", Case.Sensitive);
        read.Target.Mode.ShouldBe("agent", Case.Sensitive);
        read.AllowedTools.ShouldBe([AutomationTools.PullRequestCreate, AutomationTools.PullRequestComment]);
        read.MaxConcurrentRuns.ShouldBe(2);
        read.MaxRunsPerHour.ShouldBe(7);
        read.MaxRuntimeMinutes.ShouldBe(45);
        read.CreatedAt.ShouldBe(automation.CreatedAt);
        read.UpdatedAt.ShouldBe(automation.UpdatedAt);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task List_returns_every_definition_and_delete_removes_history_and_cursor(string kind)
    {
        using var directory = new TempDirectory();
        var (store, owner) = Open(kind, directory.Path);
        using var owned = owner;
        await store.SaveAsync(TestAutomations.Make(id: "aut_a"), Token).ConfigureAwait(true);
        await store.SaveAsync(TestAutomations.Make(id: "aut_b"), Token).ConfigureAwait(true);
        await store.UpsertRunAsync("aut_a", Run("r1", 0), Token).ConfigureAwait(true);
        await store.SetScheduleCursorAsync("aut_a", Kit.Start, Token).ConfigureAwait(true);

        (await store.ListAsync(Token).ConfigureAwait(true)).Select(static automation => automation.Id).ShouldBe(["aut_a", "aut_b"], ignoreOrder: true);
        (await store.DeleteAsync("aut_a", Token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.DeleteAsync("aut_a", Token).ConfigureAwait(true)).ShouldBeFalse();

        (await store.ListAsync(Token).ConfigureAwait(true)).Select(static automation => automation.Id).ShouldBe(["aut_b"]);
        (await store.GetAsync("aut_a", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.GetHistoryAsync("aut_a", Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.GetScheduleCursorAsync("aut_a", Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task Replacing_a_definition_keeps_its_history_and_cursor(string kind)
    {
        using var directory = new TempDirectory();
        var (store, owner) = Open(kind, directory.Path);
        using var owned = owner;
        var automation = TestAutomations.Make();
        await store.SaveAsync(automation, Token).ConfigureAwait(true);
        await store.UpsertRunAsync(automation.Id, Run("r1", 0), Token).ConfigureAwait(true);
        await store.SetScheduleCursorAsync(automation.Id, Kit.Start.AddHours(2), Token).ConfigureAwait(true);

        await store.SaveAsync(automation with { Name = "Renamed" }, Token).ConfigureAwait(true);

        (await store.GetAsync(automation.Id, Token).ConfigureAwait(true))!.Name.ShouldBe("Renamed", Case.Sensitive);
        (await store.GetHistoryAsync(automation.Id, Token).ConfigureAwait(true)).Count.ShouldBe(1);
        (await store.GetScheduleCursorAsync(automation.Id, Token).ConfigureAwait(true)).ShouldBe(Kit.Start.AddHours(2));
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task History_is_newest_first_updates_an_entry_in_place_and_stops_at_one_hundred(string kind)
    {
        using var directory = new TempDirectory();
        var (store, owner) = Open(kind, directory.Path);
        using var owned = owner;
        await store.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);
        await store.UpsertRunAsync("aut_0001", Run("first", 0, AutomationRunStatus.Running), Token).ConfigureAwait(true);
        await store.UpsertRunAsync("aut_0001", Run("second", 1), Token).ConfigureAwait(true);

        await store.UpsertRunAsync("aut_0001", Run("first", 0, AutomationRunStatus.Failed), Token).ConfigureAwait(true);

        var history = await store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true);
        history.Select(static entry => entry.RunId).ShouldBe(["second", "first"]);
        history[1].Status.ShouldBe(AutomationRunStatus.Failed);
        history[1].Error.ShouldBe("boom", Case.Sensitive);

        for (var index = 0; index < 150; index++)
        {
            await store.UpsertRunAsync("aut_0001", Run("bulk" + index, 10 + index), Token).ConfigureAwait(true);
        }

        history = await store.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true);
        history.Count.ShouldBe(AutomationLimits.MaxHistoryEntries);
        history[0].RunId.ShouldBe("bulk149", Case.Sensitive);
        history[^1].RunId.ShouldBe("bulk50", Case.Sensitive);
    }

    [Theory]
    [MemberData(nameof(StoreKinds))]
    public async Task Writes_for_an_unknown_automation_are_ignored(string kind)
    {
        using var directory = new TempDirectory();
        var (store, owner) = Open(kind, directory.Path);
        using var owned = owner;

        await store.UpsertRunAsync("aut_missing", Run("r1", 0), Token).ConfigureAwait(true);
        await store.SetScheduleCursorAsync("aut_missing", Kit.Start, Token).ConfigureAwait(true);

        (await store.GetHistoryAsync("aut_missing", Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.GetScheduleCursorAsync("aut_missing", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_file_store_survives_a_reopen_and_writes_nothing_but_the_one_file()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "nested", "automations.json");
        using (var first = new FileAutomationStore(path))
        {
            await first.SaveAsync(Complete(), Token).ConfigureAwait(true);
            await first.UpsertRunAsync("aut_0001", Run("r1", 0), Token).ConfigureAwait(true);
            await first.SetScheduleCursorAsync("aut_0001", Kit.Start.AddHours(3), Token).ConfigureAwait(true);
        }

        using var second = new FileAutomationStore(path);
        (await second.GetAsync("aut_0001", Token).ConfigureAwait(true)).ShouldNotBeNull();
        (await second.GetHistoryAsync("aut_0001", Token).ConfigureAwait(true)).ShouldHaveSingleItem().Status.ShouldBe(AutomationRunStatus.Succeeded);
        (await second.GetScheduleCursorAsync("aut_0001", Token).ConfigureAwait(true)).ShouldBe(Kit.Start.AddHours(3));
        Directory.GetFiles(Path.GetDirectoryName(path)!).ShouldBe([Path.GetFullPath(path)]);
    }

    [Fact]
    public async Task The_file_store_writes_enum_words_and_camel_case_names()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "automations.json");
        using var store = new FileAutomationStore(path);

        await store.SaveAsync(Complete(), Token).ConfigureAwait(true);

        var text = await File.ReadAllTextAsync(path, Token).ConfigureAwait(true);
        text.ShouldContain("\"flavour\": \"GitHub\"", Case.Sensitive);
        text.ShouldContain("\"maxRunsPerHour\": 7", Case.Sensitive);
        text.ShouldContain("\"version\": 1", Case.Sensitive);
    }

    [Fact]
    public async Task The_automation_file_never_holds_a_webhook_secret()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "automations.json");
        using var store = new FileAutomationStore(path);
        var service = new AutomationService(store, new InMemoryAutomationSecretStore(), new FakeTimeProvider(Kit.Start));

        var change = await service.CreateAsync(TestAutomations.Alice, TestAutomations.Draft(TestAutomations.GenericHook()), Token).ConfigureAwait(true);

        var secret = change.WebhookSecret.ShouldNotBeNull();
        var text = await File.ReadAllTextAsync(path, Token).ConfigureAwait(true);
        text.ShouldNotContain(secret, Case.Sensitive);
        text.ShouldNotContain("whsec_", Case.Sensitive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("""{"version":2,"automations":{}}""")]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"automations":{"aut_x":{"automation":null}}}""")]
    [InlineData("""{"version":1,"automations":{},"unexpected":true}""")]
    public async Task A_damaged_file_fails_closed_and_is_left_untouched(string content)
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "automations.json");
        await File.WriteAllTextAsync(path, content, Token).ConfigureAwait(true);
        using var store = new FileAutomationStore(path);

        await Should.ThrowAsync<InvalidDataException>(async () => await store.ListAsync(Token).ConfigureAwait(true)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidDataException>(async () => await store.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe(content, Case.Sensitive);
    }

    [Fact]
    public async Task A_missing_file_is_an_empty_store()
    {
        using var directory = new TempDirectory();
        using var store = new FileAutomationStore(Path.Combine(directory.Path, "never-written.json"));

        (await store.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
        File.Exists(Path.Combine(directory.Path, "never-written.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task The_file_is_owner_only_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "state", "automations.json");
        using var store = new FileAutomationStore(path);

        await store.SaveAsync(TestAutomations.Make(), Token).ConfigureAwait(true);

        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(Path.GetDirectoryName(path)!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task The_secret_file_holds_values_by_reference_survives_a_reopen_and_is_owner_only()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "secrets", "automation-secrets.json");
        using (var first = new FileAutomationSecretStore(path))
        {
            await first.SetAsync("webhook:aut_1", "whsec_one", Token).ConfigureAwait(true);
            await first.SetAsync("webhook:aut_2", "whsec_two", Token).ConfigureAwait(true);
            await first.SetAsync("webhook:aut_1", "whsec_one_rotated", Token).ConfigureAwait(true);
            await first.DeleteAsync("webhook:aut_2", Token).ConfigureAwait(true);
        }

        using var second = new FileAutomationSecretStore(path);
        (await second.GetAsync("webhook:aut_1", Token).ConfigureAwait(true)).ShouldBe("whsec_one_rotated", Case.Sensitive);
        (await second.GetAsync("webhook:aut_2", Token).ConfigureAwait(true)).ShouldBeNull();
        (await second.GetAsync("webhook:aut_3", Token).ConfigureAwait(true)).ShouldBeNull();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task A_damaged_secret_file_fails_closed()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "secrets.json");
        await File.WriteAllTextAsync(path, "[1,2,3]", Encoding.UTF8, Token).ConfigureAwait(true);
        using var store = new FileAutomationSecretStore(path);

        await Should.ThrowAsync<InvalidDataException>(async () => await store.GetAsync("webhook:x", Token).ConfigureAwait(true)).ConfigureAwait(true);
        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe("[1,2,3]", Case.Sensitive);
    }

    [Fact]
    public async Task The_in_memory_secret_store_reads_back_what_it_was_given()
    {
        var store = new InMemoryAutomationSecretStore();

        await store.SetAsync("webhook:a", "value-a", Token).ConfigureAwait(true);

        (await store.GetAsync("webhook:a", Token).ConfigureAwait(true)).ShouldBe("value-a", Case.Sensitive);
        await store.DeleteAsync("webhook:a", Token).ConfigureAwait(true);
        (await store.GetAsync("webhook:a", Token).ConfigureAwait(true)).ShouldBeNull();
        await Should.ThrowAsync<ArgumentException>(async () => await store.SetAsync("webhook:a", string.Empty, Token).ConfigureAwait(true)).ConfigureAwait(true);
    }
}
