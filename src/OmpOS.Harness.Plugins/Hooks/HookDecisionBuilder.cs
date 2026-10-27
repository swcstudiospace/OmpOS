using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Accumulates the outcomes of the hooks of one dispatch and merges them into one <see cref="HookDecision"/>.
/// </summary>
internal sealed class HookDecisionBuilder
{
    private readonly List<string> _userMessages = [];
    private readonly List<string> _agentMessages = [];
    private readonly List<string> _contexts = [];
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
    private readonly List<string> _pluginPaths = [];
    private readonly List<HookRunRecord> _records = [];
    private readonly HookOmpResultMerger _omp = new();
    private HookVerdict? _verdict;
    private HookDecisionCause _cause;
    private JsonElement? _updatedInput;
    private JsonElement? _updatedMcpToolOutput;
    private string? _followup;
    private bool? _shouldContinue;
    private int _run;

    /// <summary>The merged <c>omp</c> section result, which the dispatcher fills for synthetic denies.</summary>
    public HookOmpResultMerger Omp => _omp;

    /// <summary>
    /// Adds one hook's outcome.
    /// </summary>
    /// <param name="step">The outcome.</param>
    public void Add(HookStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _records.Add(step.Record);
        if (step.Ran)
        {
            _run++;
        }

        if (step.DenyCause != HookDecisionCause.None)
        {
            Deny(step.DenyCause);
            AddText(_agentMessages, step.DenyAgentMessage);
            AddText(_userMessages, step.DenyUserMessage);
        }

        if (step.Output is { } output)
        {
            AddOutput(output);
        }
    }

    /// <summary>
    /// Merges everything added so far.
    /// </summary>
    /// <returns>The merged decision.</returns>
    public HookDecision Build() => new()
    {
        Verdict = _verdict,
        Cause = _cause,
        UserMessage = Join(_userMessages),
        AgentMessage = Join(_agentMessages),
        UpdatedInput = _updatedInput,
        UpdatedMcpToolOutput = _updatedMcpToolOutput,
        AdditionalContext = Join(_contexts),
        FollowupMessage = _followup,
        Environment = new Dictionary<string, string>(_environment, StringComparer.Ordinal),
        ShouldContinue = _shouldContinue,
        PluginPaths = [.. _pluginPaths],
        OmpResult = _omp.Build(),
        Records = [.. _records],
        HooksRun = _run,
    };

    private void AddOutput(HookOutputParse output)
    {
        if (output.Verdict is { } verdict)
        {
            if (verdict == HookVerdict.Deny)
            {
                Deny(HookDecisionCause.HookDenied);
            }
            else if (_verdict is null || (verdict == HookVerdict.Ask && _verdict == HookVerdict.Allow))
            {
                _verdict = verdict;
            }
        }

        AddText(_userMessages, output.UserMessage);
        AddText(_agentMessages, output.AgentMessage);
        AddText(_contexts, output.AdditionalContext);
        _updatedInput = output.UpdatedInput ?? _updatedInput;
        _updatedMcpToolOutput = output.UpdatedMcpToolOutput ?? _updatedMcpToolOutput;
        if (!string.IsNullOrEmpty(output.FollowupMessage))
        {
            _followup = output.FollowupMessage;
        }

        foreach (var pair in output.Environment)
        {
            _environment[pair.Key] = pair.Value;
        }

        _pluginPaths.AddRange(output.PluginPaths);
        if (output.ShouldContinue is { } proceed)
        {
            _shouldContinue = proceed ? _shouldContinue ?? true : false;
        }

        if (output.OmpObject is { } ompObject)
        {
            _omp.Add(ompObject);
        }
    }

    private void Deny(HookDecisionCause cause)
    {
        _verdict = HookVerdict.Deny;
        if (_cause == HookDecisionCause.None)
        {
            _cause = cause;
        }
    }

    private static void AddText(List<string> target, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            target.Add(text);
        }
    }

    private static string? Join(List<string> parts) => parts.Count == 0 ? null : string.Join('\n', parts);
}
