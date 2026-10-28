using System.Collections.Frozen;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Builds the <see cref="EffectiveHookSet"/>: locates the hook files, parses each, applies scope defaults and the consent
/// identity, drops duplicates and appends the hooks of loaded plugins. It never throws for a bad file: every problem
/// becomes a <see cref="HookDiagnostic"/>. Only cancellation escapes.
/// </summary>
public sealed class HooksConfigLoader
{
    private const int MaxDigestedFileBytes = 1024 * 1024;

    // omp events whose result can stop an action. A hook on one of them denies when it has no consent, like a permission hook.
    private static readonly FrozenSet<string> GuardingOmpEvents = new[] { "tool_call", "before_subagent_spawn", "input" }
        .ToFrozenSet(StringComparer.Ordinal);

    private readonly IHookFileSource _files;

    /// <summary>
    /// Initializes a loader.
    /// </summary>
    /// <param name="files">Reads the hook files and the scripts they name.</param>
    public HooksConfigLoader(IHookFileSource files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    /// <summary>
    /// Loads every hook that applies to a workspace.
    /// </summary>
    /// <param name="context">The workspace roots, home directory and platform.</param>
    /// <param name="pluginHooks">The hooks of loaded plugin manifests, or null. They run after every file source.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The effective set. It is never null.</returns>
    public async Task<EffectiveHookSet> LoadAsync(
        HookLocatorContext context,
        IReadOnlyList<HookRegistration>? pluginHooks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sources = HookSourceLocator.Locate(context, _files);
        var diagnostics = new List<HookDiagnostic>();
        var hooks = new List<EffectiveHook>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            var document = await ReadSourceAsync(source, diagnostics, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                continue;
            }

            diagnostics.AddRange(document.Diagnostics);
            var tamperResistant = _files.Inspect(source.Path).IsTamperResistant;
            if (source.Scope == HookScope.Managed && !tamperResistant)
            {
                diagnostics.Add(new HookDiagnostic(
                    HookDiagnosticSeverity.Warning,
                    source.Path,
                    "$",
                    "The managed file or its directory is writable by group or others, or its owner cannot be checked on this platform, so its hooks are not pre-consented."));
            }

            foreach (var definition in document.Hooks.Concat(document.OmpHooks))
            {
                var hook = await BuildAsync(definition, source, context.Platform, tamperResistant, cancellationToken).ConfigureAwait(false);
                if (!seen.Add(DuplicateKey(hook)))
                {
                    diagnostics.Add(new HookDiagnostic(
                        HookDiagnosticSeverity.Warning,
                        source.Path,
                        $"$.{(hook.IsOmpEvent ? "omp" : "hooks")}.{hook.EventName}[{hook.Index}]",
                        "The same hook (event, command and working directory) is already loaded from another file and runs once."));
                    continue;
                }

                hooks.Add(hook with { Id = UniqueId(hook.Id, ids) });
            }
        }

        if (pluginHooks is not null)
        {
            foreach (var registration in pluginHooks)
            {
                hooks.Add(FromPlugin(registration) with { Id = UniqueId(registration.Id, ids) });
            }
        }

        return new EffectiveHookSet(hooks, sources, diagnostics);
    }

    private async Task<HooksConfigDocument?> ReadSourceAsync(HookSource source, List<HookDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var limit = source.Format == HookConfigFormat.HooksJson ? HookConfigLimits.MaxHooksFileBytes : HookConfigLimits.MaxClaudeSettingsBytes;
        var bytes = await _files.ReadAsync(source.Path, limit, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            diagnostics.Add(new HookDiagnostic(
                HookDiagnosticSeverity.Error,
                source.Path,
                "$",
                $"The file could not be read or is larger than {limit} bytes."));
            return null;
        }

        return HooksConfigParser.Parse(bytes, source.Format, source.Path);
    }

    private static bool IsBlocking(HookDefinition definition) => definition.IsOmpEvent
        ? GuardingOmpEvents.Contains(definition.EventName)
        : definition.Event is { } hookEvent && HookEvents.IsPermission(hookEvent);

    private async Task<EffectiveHook> BuildAsync(
        HookDefinition definition,
        HookSource source,
        HookPlatform platform,
        bool tamperResistant,
        CancellationToken cancellationToken)
    {
        var shell = definition.Type == HookType.Command && definition.Command is not null ? HookPlatforms.ShellFor(platform) : HookShell.None;
        var timeout = definition.TimeoutSeconds ?? HookConfigLimits.DefaultTimeoutSeconds;
        var loopLimit = definition.LoopLimitSpecified
            ? definition.LoopLimit
            : source.Format == HookConfigFormat.ClaudeSettings ? null : (int?)HookConfigLimits.DefaultLoopLimit;
        var digests = await DigestsAsync(definition, source, cancellationToken).ConfigureAwait(false);
        _ = HookMatcher.TryCompile(definition.Matcher, out var compiled, out var problem);
        var identity = new HookIdentityInput(
            definition.EventName,
            definition.IsOmpEvent,
            source.Scope,
            definition.Type,
            definition.Command,
            definition.Args,
            shell,
            HookConsentIdentity.RelativeDirectory(source.ScopeRoot, source.WorkingDirectory),
            definition.Env,
            definition.Prompt,
            definition.Model,
            definition.FailClosed,
            definition.Matcher,
            timeout,
            loopLimit,
            definition.RunsIn,
            digests);
        var blocking = IsBlocking(definition);
        var suffix = definition.Id ?? $"{definition.EventName}:{definition.Index}";
        return new EffectiveHook(
            $"{source.PseudoPluginId}:{suffix}",
            source.PseudoPluginId,
            source.Scope,
            source.Path,
            definition.EventName,
            definition.Event,
            definition.IsOmpEvent,
            definition.Index,
            definition.Type,
            definition.Command,
            definition.Args,
            shell,
            definition.Prompt,
            definition.Model,
            TimeSpan.FromSeconds(timeout),
            loopLimit,
            definition.FailClosed,
            definition.Matcher,
            definition.Enabled,
            definition.Description,
            definition.Env,
            definition.RunsIn,
            source.WorkingDirectory,
            source.ScopeRoot,
            HookConsentIdentity.Compute(identity),
            HookConsentIdentity.CurrentVersion,
            source.Scope == HookScope.Managed && tamperResistant,
            blocking,
            compiled,
            problem);
    }

    private async Task<IReadOnlyList<HookExecutableDigest>> DigestsAsync(HookDefinition definition, HookSource source, CancellationToken cancellationToken)
    {
        if (definition.Type != HookType.Command)
        {
            return [];
        }

        var digests = new List<HookExecutableDigest>();
        foreach (var (absolute, relative) in HookExecutableScanner.Candidates(definition.Command, definition.Args, source.WorkingDirectory, source.ScopeRoot))
        {
            var digest = await _files.DigestAsync(absolute, MaxDigestedFileBytes, cancellationToken).ConfigureAwait(false);
            if (digest is not null)
            {
                digests.Add(new HookExecutableDigest(relative, digest));
            }
        }

        return digests;
    }

    private static string DuplicateKey(EffectiveHook hook) =>
        string.Join(
            '\u001f',
            hook.IsOmpEvent ? "omp" : "cursor",
            hook.EventName,
            hook.Type,
            hook.Command ?? string.Empty,
            hook.Args is null ? string.Empty : string.Join('\u001e', hook.Args),
            hook.Prompt ?? string.Empty,
            hook.Matcher ?? string.Empty,
            hook.WorkingDirectory);

    private static string UniqueId(string id, HashSet<string> used)
    {
        var candidate = id;
        for (var suffix = 2; !used.Add(candidate); suffix++)
        {
            candidate = $"{id}~{suffix}";
        }

        return candidate;
    }

    private static EffectiveHook FromPlugin(HookRegistration registration)
    {
        var hookEvent = registration.Trigger switch
        {
            HookTrigger.SessionStart => HookEvent.SessionStart,
            HookTrigger.BeforeAction => HookEvent.PreToolUse,
            HookTrigger.AfterAction => HookEvent.PostToolUse,
            _ => HookEvent.SessionEnd,
        };
        return new EffectiveHook(
            registration.Id,
            registration.PluginId,
            HookScope.Plugin,
            registration.PluginId,
            HookEvents.Format(hookEvent),
            hookEvent,
            false,
            0,
            HookType.Command,
            null,
            registration.Command.Arguments,
            HookShell.None,
            null,
            null,
            TimeSpan.FromSeconds(HookConfigLimits.DefaultTimeoutSeconds),
            null,
            false,
            null,
            registration.Enabled,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            HookLocations.Both,
            registration.Command.WorkingDirectory,
            registration.Command.WorkingDirectory,
            registration.CommandHash,
            1,
            false,
            registration.Blocking,
            null,
            null);
    }
}
