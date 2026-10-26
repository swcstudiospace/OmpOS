using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Stages;

namespace OmpOS.Harness.AgentLoop.Connection;

/// <summary>
/// Reports, per launched plugin, whether omp actually registered it. The omp plugin writes a connection report (see
/// <see cref="PluginConnectionReport"/>) to the session's <see cref="SessionStateLayout.ConnectionFile"/>; this source polls
/// that file and compares the tool and command names in it with the names the plugin is expected to register
/// (<see cref="OmpPluginDescriptor.ExpectedTools"/> and <see cref="OmpPluginDescriptor.ExpectedCommands"/>). Each plugin gets
/// one stage, <c>connection:&lt;plugin id&gt;</c>, with the plugin's own <see cref="StageSource"/>: pending until the first
/// report, then succeeded, degraded or failed, or degraded when no report arrives within the deadline. Commands are only
/// checked when the report says omp could list them. A plugin that was not launched is not in
/// <see cref="PluginConnectionStageSourceOptions.Plugins"/> and gets no stage. The source never throws, a report that cannot be
/// read is a degraded stage, the last readable report stays in force, and the text of an event comes from the catalog's names
/// only, never from the report.
/// </summary>
public sealed class PluginConnectionStageSource : IStageSource
{
    /// <summary>The prefix of every stage identifier: the plugin identifier follows it.</summary>
    public const string StageIdPrefix = "connection:";

    /// <summary>The detail of a stage whose plugin registered every expected name.</summary>
    public const string ConnectedDetail = "Connected to omp";

    /// <summary>The detail of a stage that waits for the first report.</summary>
    public const string WaitingDetail = "Waiting for omp to report its plugins";

    /// <summary>The detail of a stage when the report file exists but cannot be used.</summary>
    public const string UnreadableDetail = "omp wrote a connection report that could not be read";

    private const int MaxListedNames = 8;

    private static readonly Action<ILogger, FileReadStatus, Exception?> ReportUnusable =
        LoggerMessage.Define<FileReadStatus>(
            LogLevel.Warning,
            new EventId(1, "PluginConnectionReportUnusable"),
            "The omp connection report could not be used ({Status}); the plugin connection stages are degraded until it can.");

    private readonly PluginConnectionStageSourceOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PluginConnectionStageSource> _logger;

    /// <summary>Creates a source.</summary>
    /// <param name="options">The plugins to check and the intervals.</param>
    /// <param name="timeProvider">The clock that stamps events and paces polling. The system clock when null.</param>
    /// <param name="logger">Receives unreadable-file diagnostics. Null discards them.</param>
    /// <exception cref="ArgumentOutOfRangeException">The poll interval or the deadline is not positive.</exception>
    public PluginConnectionStageSource(
        PluginConnectionStageSourceOptions options,
        TimeProvider? timeProvider = null,
        ILogger<PluginConnectionStageSource>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Plugins is null)
        {
            throw new ArgumentException("The options carry no plugin list.", nameof(options));
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.PollInterval, "The poll interval must be positive.");
        }

        if (options.ReportDeadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ReportDeadline, "The report deadline must be positive.");
        }

        _options = options;
        _clock = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PluginConnectionStageSource>.Instance;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var plugins = _options.Plugins;
        if (plugins.Count == 0)
        {
            yield break;
        }

        var started = _clock.GetUtcNow();
        var dedupers = new StageEventDeduper[plugins.Count];
        for (var index = 0; index < dedupers.Length; index++)
        {
            dedupers[index] = new StageEventDeduper(plugins[index].StageSource, scope.SessionId, _clock);
        }

        var state = new PollState();
        while (!cancellationToken.IsCancellationRequested)
        {
            List<StageEvent> events;
            try
            {
                await PollAsync(scope, state, cancellationToken).ConfigureAwait(false);
                events = Project(plugins, dedupers, state, started);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                state.Hash = null;
                state.Unreadable = true;
                NoteUnusable(state, FileReadStatus.Unreadable, ex);
                events = Project(plugins, dedupers, state, started);
            }

            foreach (var stageEvent in events)
            {
                yield return stageEvent;
            }

            try
            {
                await Task.Delay(_options.PollInterval, _clock, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private static string StageIdOf(OmpPluginDescriptor descriptor) => StageIdPrefix + descriptor.Id;

    private static string TitleOf(OmpPluginDescriptor descriptor) => descriptor.DisplayName + " connection";

    private static string NormalizeCommand(string name)
    {
        var trimmed = name.Trim();
        return trimmed.StartsWith('/') ? trimmed[1..] : trimmed;
    }

    private static StageSnapshot Evaluate(OmpPluginDescriptor descriptor, PluginConnectionReport report)
    {
        var tools = new HashSet<string>(report.Tools.Select(static name => name.Trim()), StringComparer.Ordinal);
        var commands = new HashSet<string>(StringComparer.Ordinal);
        if (report.CommandsAvailable)
        {
            foreach (var command in report.Commands)
            {
                commands.Add(NormalizeCommand(command));
            }
        }

        var expected = 0;
        var missing = new List<string>();
        foreach (var tool in descriptor.ExpectedTools)
        {
            expected++;
            if (!tools.Contains(tool))
            {
                missing.Add(tool);
            }
        }

        if (report.CommandsAvailable)
        {
            foreach (var command in descriptor.ExpectedCommands)
            {
                expected++;
                if (!commands.Contains(command))
                {
                    missing.Add("/" + command);
                }
            }
        }
        else if (descriptor.ExpectedTools.Count == 0 && descriptor.ExpectedCommands.Count > 0)
        {
            return Snapshot(descriptor, StageStatus.Degraded, "omp could not list its commands, so " + descriptor.DisplayName + " could not be checked");
        }

        if (missing.Count == 0)
        {
            return Snapshot(descriptor, StageStatus.Succeeded, ConnectedDetail);
        }

        var listed = string.Join(", ", missing.Take(MaxListedNames));
        if (missing.Count > MaxListedNames)
        {
            listed += string.Create(CultureInfo.InvariantCulture, $", and {missing.Count - MaxListedNames} more");
        }

        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"Registered {expected - missing.Count} of {expected} expected names; missing: {listed}");
        return Snapshot(descriptor, missing.Count == expected ? StageStatus.Failed : StageStatus.Degraded, detail);
    }

    private static StageSnapshot Snapshot(OmpPluginDescriptor descriptor, StageStatus status, string detail) =>
        new(StageIdOf(descriptor), TitleOf(descriptor), status, StageText.Clean(detail));

    private List<StageEvent> Project(
        IReadOnlyList<OmpPluginDescriptor> plugins,
        StageEventDeduper[] dedupers,
        PollState state,
        DateTimeOffset started)
    {
        var elapsed = _clock.GetUtcNow() - started;
        var events = new List<StageEvent>();
        for (var index = 0; index < plugins.Count; index++)
        {
            events.AddRange(dedupers[index].Filter([SnapshotOf(plugins[index], state, elapsed)]));
        }

        return events;
    }

    private StageSnapshot SnapshotOf(OmpPluginDescriptor descriptor, PollState state, TimeSpan elapsed)
    {
        if (state.Report is { } report)
        {
            return Evaluate(descriptor, report);
        }

        if (state.Unreadable)
        {
            return Snapshot(descriptor, StageStatus.Degraded, UnreadableDetail);
        }

        if (elapsed >= _options.ReportDeadline)
        {
            var seconds = (int)Math.Ceiling(_options.ReportDeadline.TotalSeconds);
            return Snapshot(
                descriptor,
                StageStatus.Degraded,
                string.Create(CultureInfo.InvariantCulture, $"omp has not reported its plugins after {seconds} s"));
        }

        return Snapshot(descriptor, StageStatus.Pending, WaitingDetail);
    }

    private async Task PollAsync(StageObservationScope scope, PollState state, CancellationToken cancellationToken)
    {
        string path;
        try
        {
            path = SessionStateLayout.ConnectionFile(scope.SessionStateDirectory);
        }
        catch (ArgumentException)
        {
            state.Unreadable = true;
            return;
        }

        var read = await StageFileReader.ReadAllAsync(path, PluginConnectionReport.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        switch (read.Status)
        {
            case FileReadStatus.Missing:
                return;
            case FileReadStatus.Ok:
                break;
            default:
                state.Hash = null;
                state.Unreadable = true;
                NoteUnusable(state, read.Status, read.Error);
                return;
        }

        var hash = SHA256.HashData(read.Content.Span);
        if (state.Hash is not null && hash.AsSpan().SequenceEqual(state.Hash))
        {
            return;
        }

        state.Hash = hash;
        var report = PluginConnectionReport.TryParse(read.Content.Span);
        if (report is null)
        {
            state.Unreadable = true;
            NoteUnusable(state, FileReadStatus.Unreadable, null);
            return;
        }

        state.Report = report;
        state.Unreadable = false;
        state.LoggedUnusable = false;
    }

    private void NoteUnusable(PollState state, FileReadStatus status, Exception? error)
    {
        if (state.LoggedUnusable)
        {
            return;
        }

        state.LoggedUnusable = true;
        ReportUnusable(_logger, status, error);
    }

    private sealed class PollState
    {
        public byte[]? Hash { get; set; }

        public PluginConnectionReport? Report { get; set; }

        public bool Unreadable { get; set; }

        public bool LoggedUnusable { get; set; }
    }
}
