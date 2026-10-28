using System.Globalization;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Substrate;

namespace Autonogrammer.Harness.AgentLoop.Gsd;

/// <summary>
/// Builds the <see cref="TaskContext"/> of a session from the workflow phase of the session's workspace and, when one is
/// supplied, the substrate's snapshot. Each part is optional: a part that cannot be read is left out and a plain-text reason is
/// appended to <see cref="TaskContext.Warnings"/>. Only the workspace root of the scope is read, so a different scope yields
/// that scope's own context. Cancellation throws <see cref="OperationCanceledException"/>; nothing else escapes.
/// </summary>
public sealed class GsdTaskContextSource : ITaskContextSource
{
    private const string UntitledTask = "Untitled task";

    private readonly GsdToolsClient _client;
    private readonly ISubstrateContextReader? _substrateReader;

    /// <summary>Initializes a new instance of the <see cref="GsdTaskContextSource"/> class.</summary>
    /// <param name="client">Reads the workflow phase.</param>
    /// <param name="substrateReader">Reads the substrate part of the context, or null when no substrate is configured.</param>
    public GsdTaskContextSource(GsdToolsClient client, ISubstrateContextReader? substrateReader = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _substrateReader = substrateReader;
    }

    /// <inheritdoc />
    public async Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<string>();
        var phase = await ReadPhaseAsync(scope, warnings, cancellationToken).ConfigureAwait(false);
        var substrate = await ReadSubstrateAsync(scope, warnings, cancellationToken).ConfigureAwait(false);

        var substrateTitle = substrate?.Title;
        var title = !string.IsNullOrWhiteSpace(substrateTitle)
            ? substrateTitle
            : phase is null
                ? UntitledTask
                : string.Create(CultureInfo.InvariantCulture, $"Phase {phase.Number}: {phase.Name}");

        return new TaskContext
        {
            Title = title,
            TaskId = substrate?.TaskId,
            KanbanStatus = substrate?.KanbanStatus,
            GraphId = scope.GraphId,
            Phase = phase,
            SharedMemory = substrate?.SharedMemory ?? [],
            Lessons = substrate?.Lessons ?? [],
            Warnings = warnings,
        };
    }

    private async Task<GsdPhaseInfo?> ReadPhaseAsync(StageObservationScope scope, List<string> warnings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scope.WorkspaceRoot))
        {
            warnings.Add("Workflow phase is unknown: the session has no workspace.");
            return null;
        }

        try
        {
            var snapshot = await _client.GetWorkflowAsync(scope.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
            if (snapshot.CurrentPhase is null)
            {
                warnings.Add("Workflow phase is unknown: the roadmap lists no phases.");
            }

            return snapshot.CurrentPhase;
        }
        catch (GsdToolsException exception)
        {
            warnings.Add("Workflow phase is unknown: " + exception.Message + ".");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            warnings.Add("Workflow phase is unknown: the read was interrupted.");
            return null;
        }
    }

    private async Task<SubstrateContextSnapshot?> ReadSubstrateAsync(StageObservationScope scope, List<string> warnings, CancellationToken cancellationToken)
    {
        if (_substrateReader is null)
        {
            return null;
        }

        try
        {
            var snapshot = await _substrateReader.ReadAsync(scope, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                warnings.Add("Substrate context is unavailable: the reader returned nothing.");
                return null;
            }

            warnings.AddRange(snapshot.Warnings);
            return snapshot;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            warnings.Add("Substrate context is unavailable: the read was interrupted.");
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The exception message is not copied: a reader's failure text may quote a response and must not reach the UI.
            warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Substrate context is unavailable: the reader failed ({exception.GetType().Name})."));
            return null;
        }
    }
}
