using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Controls;

namespace OmpOS.Web.Demo;

/// <summary>Static data of the Demo: the model list, the engine options, the engine commands, the plugin stages and the task context.</summary>
internal static class DemoCatalogue
{
    /// <summary>The model every Demo session starts on.</summary>
    internal const string DefaultModel = "anthropic/claude-sonnet-4-5";

    /// <summary>
    /// The models the Demo offers. The provider ids (<c>anthropic</c>, <c>openai</c>, <c>google</c>, <c>deepseek</c>, <c>mistral</c>, <c>xai</c>, <c>moonshot</c>,
    /// <c>zai</c>) are provider rows of the OmpOS provider catalogue (<c>docs/reference/providers.md</c>); the model names are illustrative.
    /// </summary>
    internal static IReadOnlyList<ConfigOptionValue> Models { get; } =
    [
        new() { Value = DefaultModel, Name = "Claude Sonnet 4.5", Description = "Anthropic API" },
        new() { Value = "anthropic/claude-opus-4-1", Name = "Claude Opus 4.1", Description = "Anthropic API" },
        new() { Value = "openai/gpt-5-codex", Name = "GPT-5 Codex", Description = "OpenAI API" },
        new() { Value = "google/gemini-2.5-pro", Name = "Gemini 2.5 Pro", Description = "Gemini API (AI Studio)" },
        new() { Value = "deepseek/deepseek-chat", Name = "DeepSeek Chat", Description = "DeepSeek API" },
        new() { Value = "mistral/codestral-latest", Name = "Codestral", Description = "Mistral AI La Plateforme" },
        new() { Value = "xai/grok-code-fast-1", Name = "Grok Code Fast", Description = "xAI Grok API" },
        new() { Value = "moonshot/kimi-k2", Name = "Kimi K2", Description = "Kimi API Platform (global)" },
        new() { Value = "zai/glm-4.6", Name = "GLM 4.6", Description = "GLM Coding Plan (global, Z.ai)" },
    ];

    /// <summary>The engine commands the Demo advertises.</summary>
    internal static IReadOnlyList<SessionCommand> Commands { get; } =
    [
        new() { Name = "compact", Description = "Summarise the conversation to free context", InputHint = "[focus]" },
        new() { Name = "plan", Description = "Switch to plan mode" },
        new() { Name = "model", Description = "Choose the model", InputHint = "<model>" },
        new() { Name = "review", Description = "Review the uncommitted changes" },
        new() { Name = "init", Description = "Write AGENTS.md for this repository" },
        new() { Name = "memory", Description = "Show what the agent remembers", InputHint = "[query]" },
        new() { Name = "undo", Description = "Undo the last edit" },
        new() { Name = "redo", Description = "Redo the last undone edit" },
        new() { Name = "diff", Description = "Show the diff of this session" },
        new() { Name = "status", Description = "Show the session status" },
        new() { Name = "help", Description = "List the commands" },
        new() { Name = "clear", Description = "Start a fresh conversation" },
        new() { Name = "export", Description = "Export the transcript", InputHint = "[path]" },
        new() { Name = "resume", Description = "Resume an earlier session" },
        new() { Name = "fork", Description = "Fork the conversation here" },
        new() { Name = "mcp", Description = "List the connected MCP servers" },
        new() { Name = "hooks", Description = "List the active hooks" },
        new() { Name = "skills", Description = "List the available skills" },
        new() { Name = "cost", Description = "Show token use and cost" },
        new() { Name = "doctor", Description = "Check the setup" },
    ];

    /// <summary>Builds the model, mode and thinking options.</summary>
    /// <param name="model">The current model.</param>
    /// <param name="mode">The current mode.</param>
    /// <param name="thinking">The current thinking level.</param>
    /// <returns>The options.</returns>
    internal static List<ConfigOption> Options(string model = DefaultModel, string mode = "default", string thinking = "medium") =>
    [
        new() { Id = "model", Name = "Model", Category = "model", CurrentValue = model, Values = Models },
        new()
        {
            Id = "mode",
            Name = "Mode",
            Category = "mode",
            CurrentValue = mode,
            Values =
            [
                new() { Value = "default", Name = "Default", Description = "The agent edits files and runs tools after you approve them." },
                new() { Value = "plan", Name = "Plan", Description = "The agent proposes a plan and changes nothing." },
            ],
        },
        new()
        {
            Id = "thinking",
            Name = "Thinking",
            Category = "thought_level",
            CurrentValue = thinking,
            Values =
            [
                new() { Value = "low", Name = "Low", Description = "Quick answers." },
                new() { Value = "medium", Name = "Medium", Description = "Balanced." },
                new() { Value = "high", Name = "High", Description = "Thinks longer before answering." },
            ],
        },
    ];

    /// <summary>Builds the four plugin connection stages (all connected) and the engine turn stage of a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turn">The status of the engine turn.</param>
    /// <param name="turnDetail">The detail of the engine turn, or null.</param>
    /// <param name="now">The stage time.</param>
    /// <returns>The stages.</returns>
    internal static List<StageEvent> Stages(string sessionId, StageStatus turn, string? turnDetail, DateTimeOffset now) =>
    [
        new() { SessionId = sessionId, Source = StageSource.Omp, StageId = "omp/turn", Title = "Agent turn", Status = turn, Timestamp = now, Detail = turnDetail },
        Plugin(sessionId, StageSource.Ultrathink, "Ultrathink", now),
        Plugin(sessionId, StageSource.Swarm, "Agent Swarm", now),
        Plugin(sessionId, StageSource.Substrate, "Substrate", now),
        Plugin(sessionId, StageSource.Gsd, "GSD-Core", now),
    ];

    /// <summary>Builds the task context of the sessions bound to the checkout task.</summary>
    /// <param name="now">The time notes were recorded.</param>
    /// <returns>The context.</returns>
    internal static TaskContext CheckoutContext(DateTimeOffset now) => new()
    {
        Title = "Fix the checkout total rounding",
        TaskId = "TASK-142",
        KanbanStatus = "in-progress",
        GraphId = "graph-checkout-142",
        Phase = new GsdPhaseInfo { Number = "4", Name = "Checkout", Status = "executing", PlansComplete = 2, PlansTotal = 3 },
        SharedMemory =
        [
            new() { Id = "mem-1", Text = "Prices are stored as decimal with two fraction digits. Never use double.", Origin = "substrate", RecordedAt = now.AddDays(-9) },
            new() { Id = "mem-2", Text = "The shop rounds half up (away from zero) to the cent.", Origin = "substrate", RecordedAt = now.AddDays(-3) },
        ],
        Lessons =
        [
            new() { Id = "les-1", Text = "Add a regression test with the exact failing value before changing the rounding.", Origin = "gsd", RecordedAt = now.AddDays(-14) },
        ],
        Warnings = [],
    };

    private static StageEvent Plugin(string sessionId, StageSource source, string title, DateTimeOffset now) => new()
    {
        SessionId = sessionId,
        Source = source,
        StageId = "plugin/" + title.ToUpperInvariant().Replace(' ', '-'),
        Title = title + " connected",
        Status = StageStatus.Succeeded,
        Timestamp = now,
        Detail = "Connected (Demo).",
    };
}
