using Autonogrammer.Native.Composition.Checkpoints;
using Autonogrammer.Workspace;
using Autonogrammer.Worktrees;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Checkpoints;

/// <summary>The consent rule for a worktree setup: a stored approval skips the question, only a yes is recorded, a failing prompt refuses.</summary>
public sealed class WorktreeSetupConsentAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-consent-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorktreeSetupConsentRequest Request(params string[] commands)
    {
        var file = Path.Combine(_root, ".cursor", "worktrees.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{}");
        var setup = new CursorWorktreeSetup(commands, null);
        var config = CursorWorktreesConfig.Parse("{}", file);
        return new WorktreeSetupConsentRequest(WorktreeSetupConsentIdentity.Create(_root, config, setup), ".cursor/worktrees.json", setup);
    }

    /// <summary>A stored approval answers yes and nothing is asked.</summary>
    [Fact]
    public async Task AStoredApprovalSkipsThePrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FakeStore { Granted = true };
        var prompt = new FakePrompt(SetupConsentDecision.Granted());
        var adapter = new WorktreeSetupConsentAdapter(store, prompt);

        var granted = await adapter.IsGrantedAsync(Request("npm ci"), ct).ConfigureAwait(true);

        granted.ShouldBeTrue();
        prompt.Asked.ShouldBeEmpty();
        store.Recorded.ShouldBe(0);
    }

    /// <summary>A refusal is not recorded.</summary>
    [Fact]
    public async Task ADeniedPromptRecordsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FakeStore();
        var prompt = new FakePrompt(SetupConsentDecision.Denied("no"));
        var adapter = new WorktreeSetupConsentAdapter(store, prompt);

        var granted = await adapter.IsGrantedAsync(Request("npm ci"), ct).ConfigureAwait(true);

        granted.ShouldBeFalse();
        store.Recorded.ShouldBe(0);
        prompt.Asked.Count.ShouldBe(1);
    }

    /// <summary>A yes is recorded once and the prompt shows the exact commands and the root path variable.</summary>
    [Fact]
    public async Task AGrantedPromptIsRecordedOnceAndDescribesTheCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FakeStore();
        var prompt = new FakePrompt(SetupConsentDecision.Granted());
        var adapter = new WorktreeSetupConsentAdapter(store, prompt);
        var request = Request("npm ci", "echo hi");

        var granted = await adapter.IsGrantedAsync(request, ct).ConfigureAwait(true);

        granted.ShouldBeTrue();
        store.Recorded.ShouldBe(1);
        var asked = prompt.Asked.Single();
        asked.Key.RepositoryPath.ShouldBe(request.Identity.StoreRepositoryPath);
        asked.Key.SetupSha256.ShouldBe(request.Identity.Sha256);
        asked.Setup.Steps.Select(static step => step.Arguments[1]).ShouldBe(["npm ci", "echo hi"]);
        asked.Setup.Environment["ROOT_WORKTREE_PATH"].ShouldBe(_root);
    }

    /// <summary>A prompt that throws or is cancelled is a refusal, and nothing is recorded.</summary>
    [Fact]
    public async Task AFailingOrCancelledPromptRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new FakeStore();
        var throwing = new WorktreeSetupConsentAdapter(store, new FakePrompt(new InvalidOperationException("dialog closed")));
        var cancelled = new WorktreeSetupConsentAdapter(store, new FakePrompt(new OperationCanceledException()));

        (await throwing.IsGrantedAsync(Request("npm ci"), ct).ConfigureAwait(true)).ShouldBeFalse();
        (await cancelled.IsGrantedAsync(Request("npm ci"), ct).ConfigureAwait(true)).ShouldBeFalse();

        store.Recorded.ShouldBe(0);
    }

    private sealed class FakeStore : ISetupConsentStore
    {
        public bool Granted { get; init; }

        public int Recorded { get; private set; }

        public Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken) => Task.FromResult(Granted);

        public Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken)
        {
            Recorded++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePrompt : ISetupConsentPrompt
    {
        private readonly SetupConsentDecision? _decision;
        private readonly Exception? _failure;

        public FakePrompt(SetupConsentDecision decision) => _decision = decision;

        public FakePrompt(Exception failure) => _failure = failure;

        public List<SetupConsentRequest> Asked { get; } = [];

        public Task<SetupConsentDecision> AskAsync(SetupConsentRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return _failure is null ? Task.FromResult(_decision!) : Task.FromException<SetupConsentDecision>(_failure);
        }
    }
}
