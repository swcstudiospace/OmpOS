using OmpOS.Native.Agents;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Automations;

namespace OmpOS.Native.Composition.Automations;

/// <summary>One session of an automation run in the head: the confirmation is an approval card of the session, the prompt is its first turn, and the turn's end ends the run.</summary>
internal sealed class HeadAutomationSession : IAutomationSession
{
    private readonly SessionSlot _slot;
    private readonly AgentSessionRuntime _runtime;
    private readonly SessionEngineSet _engines;
    private readonly Action<Action>? _dispatch;

    /// <summary>Initializes a new instance of the <see cref="HeadAutomationSession"/> class.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="slot">The session's slot.</param>
    /// <param name="runtime">The session's runtime, opened but not selected.</param>
    /// <param name="engines">The engine set, which closes the session.</param>
    /// <param name="dispatch">Runs work on the context that owns the presentation models, or null to run it inline.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="dispatch"/> is null.</exception>
    public HeadAutomationSession(string id, SessionSlot slot, AgentSessionRuntime runtime, SessionEngineSet engines, Action<Action>? dispatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(engines);
        Id = id;
        _slot = slot;
        _runtime = runtime;
        _engines = engines;
        _dispatch = dispatch;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>Runs a piece of work on the context that owns the presentation models and returns its result.</summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="dispatch">The dispatcher, or null to run inline.</param>
    /// <param name="work">The work.</param>
    /// <returns>A task with the result of the work, or its exception.</returns>
    internal static Task<T> OnUiAsync<T>(Action<Action>? dispatch, Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (dispatch is null)
        {
            return work();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatch(() => _ = RunAsync(work, completion));
        return completion.Task;
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(AutomationConfirmation request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestId = "automation-consent-" + Guid.NewGuid().ToString("N");
        var answer = _slot.ExpectConsentAsync(requestId);
        await OnUiAsync(
            _dispatch,
            () =>
            {
                _runtime.RunControls.Approvals.Add(new PendingToolApproval(Id, requestId, request.Title, request.Summary));
                return Task.FromResult(true);
            }).ConfigureAwait(false);
        try
        {
            return await answer.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Nobody answers a card of a run that ended: the answer is a denial, so the slot forgets the question.
            _ = _slot.TryDecideConsent(requestId, allow: false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<AutomationSessionTurn> RunAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var finished = new TaskCompletionSource<AutomationSessionTurn>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnTurn(object? sender, SessionTurnEventArgs e)
        {
            // The reason of a fault is not copied: it can carry text from the engine that the run history must not hold.
            _ = e.Kind switch
            {
                SessionTurnKind.Completed => finished.TrySetResult(AutomationSessionTurn.Done),
                SessionTurnKind.Faulted => finished.TrySetResult(AutomationSessionTurn.Failed(AutomationsCopy.TurnFailed)),
                _ => false,
            };
        }

        await OnUiAsync(
            _dispatch,
            () =>
            {
                _runtime.TurnChanged += OnTurn;
                return Task.FromResult(true);
            }).ConfigureAwait(false);
        try
        {
            var accepted = await OnUiAsync(_dispatch, () => _runtime.StartTurnAsync(prompt, CancellationToken.None)).ConfigureAwait(false);
            if (!accepted)
            {
                return AutomationSessionTurn.Failed("The session did not accept the prompt.");
            }

            try
            {
                return await finished.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The run timed out or the head is closing: the turn is stopped so the agent does not keep working.
                await OnUiAsync(_dispatch, async () =>
                {
                    await _runtime.Composer.StopAsync().ConfigureAwait(true);
                    return true;
                }).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            await OnUiAsync(
                _dispatch,
                () =>
                {
                    _runtime.TurnChanged -= OnTurn;
                    return Task.FromResult(true);
                }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task CloseAsync() => OnUiAsync(_dispatch, async () =>
    {
        await _engines.CloseSessionAsync(Id, CancellationToken.None).ConfigureAwait(true);
        return true;
    });

    private static async Task RunAsync<T>(Func<Task<T>> work, TaskCompletionSource<T> completion)
    {
        try
        {
            completion.TrySetResult(await work().ConfigureAwait(true));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            completion.TrySetException(exception);
        }
    }
}
