using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Everything a dispatcher test needs: a temporary state store, a manual clock, a scripted runner and a log. Hooks are
/// consented by their hash unless a test says otherwise.
/// </summary>
internal sealed class DispatcherRig : IDisposable
{
    private readonly TempDirectory _directory;

    private DispatcherRig(TempDirectory directory, PluginStateStore state, ManualTimeProvider clock, ScriptedCommandRunner runner, HookRunLog log, HookDispatcher dispatcher, HookSession session)
    {
        _directory = directory;
        State = state;
        Clock = clock;
        Runner = runner;
        Log = log;
        Dispatcher = dispatcher;
        Session = session;
    }

    /// <summary>The state store with consent and quarantine.</summary>
    public PluginStateStore State { get; }

    /// <summary>The manual clock.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>The scripted runner.</summary>
    public ScriptedCommandRunner Runner { get; }

    /// <summary>The in-memory run log.</summary>
    public HookRunLog Log { get; }

    /// <summary>The dispatcher under test.</summary>
    public HookDispatcher Dispatcher { get; }

    /// <summary>The conversation hooks run for.</summary>
    public HookSession Session { get; }

    /// <summary>The parent environment every dispatch builds the child environment from.</summary>
    public static IReadOnlyDictionary<string, string> ParentEnvironment { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin:/bin",
        ["HOME"] = "/home/ada",
        ["LC_ALL"] = "C.UTF-8",
        ["AUTONOGRAMMER_CANARY_SECRET"] = "canary-secret-value",
        ["ANTHROPIC_API_KEY"] = "sk-canary",
        ["AUTONOGRAMMER_HOOKS_TOKEN"] = "bridge-token-canary",
    };

    /// <summary>
    /// Creates a rig around a set of hooks.
    /// </summary>
    /// <param name="hooks">The hooks, in merge order.</param>
    /// <param name="cancellationToken">The test's token.</param>
    /// <param name="runner">The scripted runner; a runner that exits 0 silently when null.</param>
    /// <param name="consent">True to record consent for every hook's hash.</param>
    /// <param name="prompts">The prompt evaluator, or null.</param>
    /// <param name="options">Dispatcher options; the parent environment is always the fixed one above.</param>
    /// <param name="session">The session; a local session with one root when null.</param>
    public static async Task<DispatcherRig> CreateAsync(
        IReadOnlyList<EffectiveHook> hooks,
        CancellationToken cancellationToken,
        ScriptedCommandRunner? runner = null,
        bool consent = true,
        IPromptHookEvaluator? prompts = null,
        HookDispatcherOptions? options = null,
        HookSession? session = null)
    {
        var directory = new TempDirectory();
        try
        {
            var state = await PluginStateStore.OpenAsync(directory.StatePath, null, cancellationToken).ConfigureAwait(true);
            if (consent)
            {
                foreach (var hook in hooks)
                {
                    await state.RecordConsentAsync(hook.CommandHash, cancellationToken).ConfigureAwait(true);
                }
            }

            var clock = new ManualTimeProvider();
            var scripted = runner ?? new ScriptedCommandRunner();
            var log = new HookRunLog();
            var dispatcher = new HookDispatcher(
                new EffectiveHookSet(hooks, [], []),
                state,
                scripted,
                log,
                prompts,
                clock,
                (options ?? new HookDispatcherOptions()) with { ParentEnvironment = ParentEnvironment });
            return new DispatcherRig(directory, state, clock, scripted, log, dispatcher, session ?? HookFixtures.Session());
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Dispatches a Cursor event with the rig's session.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <param name="payload">The payload JSON.</param>
    /// <param name="cancellationToken">The test's token.</param>
    public Task<HookDecision> DispatchAsync(HookEvent hookEvent, string payload, CancellationToken cancellationToken) =>
        Dispatcher.DispatchAsync(hookEvent, Session, HookFixtures.Json(payload), cancellationToken);

    /// <summary>Releases the state store and the temporary directory.</summary>
    public void Dispose()
    {
        State.Dispose();
        Log.Dispose();
        _directory.Dispose();
    }
}
