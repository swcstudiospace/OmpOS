using System.Diagnostics;
using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
 
 using System.Diagnostics;
 using System.Text.Json;
 using Autonogrammer.Cowork.Abstractions;
 using Microsoft.Extensions.Logging;
 
 namespace Autonogrammer.Cowork.Tools;

namespace Autonogrammer.Cowork.Tools;

/// <summary>
/// The one seam through which Artifacts, Browser Use and Computer Use reach omp.
/// InvokeAsync runs the eight steps in order and does not skip a step that applies to a call the host accepted.
/// </summary>
public sealed partial class CoworkToolHost : ICoworkToolHost
{
    /// <summary>The most bytes of JSON arguments a call may carry.</summary>
    public const int MaxArgumentBytes = 256 * 1024;

    private readonly IReadOnlyList<ICoworkToolProvider> _providers;
    private readonly ICoworkPolicyGate _gate;
    private readonly ICoworkApprovalBroker _broker;
    private readonly ICoworkGrantStore _grants;
    private readonly ICoworkToolAudit _audit;
    private readonly CoworkToolHostOptions _options;
    private readonly ILogger<CoworkToolHost>? _logger;
    private readonly Dictionary<string, SessionLease> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the host.
    /// </summary>
    /// <param name="providers">The registered providers. None of them talks to omp.</param>
    /// <param name="gate">The policy gate.</param>
    /// <param name="broker">The approval broker.</param>
    /// <param name="grants">The grant store. An allow-always answer writes here only for ByPolicy.</param>
    /// <param name="audit">The audit. A write failure never fails the call.</param>
    /// <param name="options">Timeouts and the admitted profile. Null uses the defaults.</param>
    /// <param name="logger">Optional logger. Provider faults are logged by type name, never with arguments.</param>
    public CoworkToolHost(
        IReadOnlyList<ICoworkToolProvider> providers,
        ICoworkPolicyGate gate,
        ICoworkApprovalBroker broker,
        ICoworkGrantStore grants,
        ICoworkToolAudit audit,
        CoworkToolHostOptions? options = null,
        ILogger<CoworkToolHost>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(audit);
        _providers = providers;
        _gate = gate;
        _broker = broker;
        _grants = grants;
        _audit = audit;
        _options = options ?? new CoworkToolHostOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<CoworkToolEvent>? ToolEvent;

    /// <inheritdoc />
    public ICoworkSessionLease BeginSession(CoworkSessionContext session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new ArgumentException("Session id is required.", nameof(session));
        }
        lock (_sessions)
        {
            if (_sessions.ContainsKey(session.SessionId))
            {
                throw new InvalidOperationException("A Cowork session with this id is already open.");
            }

            var lease = new SessionLease(this, session);
            _sessions.Add(session.SessionId, lease);
            return lease;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CoworkToolDescriptor>> ListOfferedAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetLease(sessionId, out var lease))
        {
            return [];
        }

        var offered = new List<CoworkToolDescriptor>();
        foreach (var provider in _providers)
        {
            if (!Admits(provider.ProviderId))
            {
                continue;
            }

            CoworkProviderState state;
#pragma warning disable CA1031 // A provider fault is not an offer. Arguments are not involved.
            try
            {
                state = await provider.GetStateAsync(lease.Session, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsProviderFault(exception))
            {
                LogFault(provider.ProviderId, exception);
                continue;
            }
#pragma warning restore CA1031

            if (state.Availability != CoworkProviderAvailability.Available)
            {
                continue;
            }

            foreach (var descriptor in provider.Describe())
            {
                if (!string.Equals(descriptor.ProviderId, provider.ProviderId, StringComparison.Ordinal) || !CoworkDescriptorValidator.TryValidate(descriptor, out _))
                {
                    continue;
                }

                if (descriptor.Results.HasFlag(CoworkResultKinds.Image) && !lease.Session.ModelReadsImages)
                {
                    continue;
                }

                if (offered.TrueForAll(item => !string.Equals(item.Name, descriptor.Name, StringComparison.Ordinal)))
                {
                    offered.Add(descriptor);
                }
            }
        }

        return offered;
    }

    /// <inheritdoc />
#pragma warning disable MA0051 // The eight steps stay in one method so a step cannot be skipped by a helper returning early.
    public async ValueTask<CoworkToolResult> InvokeAsync(
        string sessionId,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetLease(sessionId, out var lease))
        {
            return CoworkToolResult.Failure(CoworkToolMessages.SessionMissing);
        }

        await lease.Call.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.StartNew();
        var callId = Guid.NewGuid().ToString("N");
        string providerId = "";
        CoworkPolicyOutcome policy = CoworkPolicyOutcome.Deny;
        CoworkApprovalOutcome? approval = null;
        string? target = null;
        CoworkResultKinds kinds = CoworkResultKinds.Text;
        CoworkToolResult? result = null;
        try
        {
            var offered = await ListOfferedAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var descriptor = offered.FirstOrDefault(item => string.Equals(item.Name, toolName, StringComparison.Ordinal));
            if (descriptor is null)
            {
                result = CoworkToolResult.Failure(CoworkToolMessages.NotOffered);
                Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.Denied, null);
                return result;
            }

            providerId = descriptor.ProviderId;
            if (arguments.ValueKind != JsonValueKind.Object || Utf8Bytes(arguments) > MaxArgumentBytes)
            {
                result = CoworkToolResult.Failure(CoworkToolMessages.ArgumentsInvalid);
                Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.Failed, null);
                return result;
            }

            var invocation = new CoworkToolInvocation(sessionId, callId, toolName, arguments, lease.Origin);
            Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.Started, null);
            var provider = ProviderFor(providerId);
            CoworkCallPreview preview;
#pragma warning disable CA1031 // A provider fault becomes a fixed failure. The log names the type, never the arguments.
            try
            {
                preview = await provider.PreviewAsync(lease.Session, invocation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsProviderFault(exception))
            {
                LogFault(toolName, exception);
                result = FaultResult(exception, cancellationToken);
                Raise(sessionId, callId, providerId, toolName, ResultKind(result), null);
                return result;
            }
#pragma warning restore CA1031

            target = preview.Target;
            CoworkPolicyDecision decision;
#pragma warning disable CA1031 // A gate fault becomes a fixed failure and never grants.
            try
            {
                decision = await _gate.DecideAsync(
                    new CoworkPolicyRequest(lease.Session, descriptor, invocation, preview, lease.IsTainted),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsProviderFault(exception))
            {
                LogFault(toolName, exception);
                result = FaultResult(exception, cancellationToken);
                Raise(sessionId, callId, providerId, toolName, ResultKind(result), target);
                return result;
            }
#pragma warning restore CA1031

            policy = decision.Outcome;
            if (decision.Outcome == CoworkPolicyOutcome.Deny)
            {
                approval = CoworkApprovalOutcome.Denied;
                result = CoworkToolResult.Failure(CoworkToolMessages.NotApproved);
                Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.Denied, target);
                return result;
            }

            if (decision.Outcome == CoworkPolicyOutcome.Ask)
            {
                Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.ApprovalRequested, target);
                var ask = await AskAsync(
                    new CoworkApprovalRequest(sessionId, callId, toolName, preview, lease.Origin, decision.MustBeLocal),
                    cancellationToken).ConfigureAwait(false);
                approval = ask;
                if (ask is not CoworkApprovalOutcome.Allowed and not CoworkApprovalOutcome.AllowedAlways
                    || (decision.MustBeLocal && lease.Origin == CoworkTurnOrigin.Dispatch))
                {
                    result = CoworkToolResult.Failure(CoworkToolMessages.NotApproved);
                    Raise(sessionId, callId, providerId, toolName, CoworkToolEventKind.Denied, target);
                    return result;
                }

                if (ask == CoworkApprovalOutcome.AllowedAlways
                    && descriptor.Approval == CoworkApprovalRequirement.ByPolicy
                    && descriptor.Effect != CoworkToolEffect.Destructive
                    && preview.Grant is { } grant)
                {
                    await _grants.GrantAsync(grant, cancellationToken).ConfigureAwait(false);
                }
            }

            using var callTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            callTimeout.CancelAfter(_options.CallTimeout);
#pragma warning disable CA1031 // A provider fault becomes a fixed failure. The log names the type, never the arguments.
            try
            {
                result = await provider.InvokeAsync(lease.Session, invocation, callTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsProviderFault(exception))
            {
                LogFault(toolName, exception);
                result = callTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? CoworkToolResult.Failure(CoworkToolMessages.TimedOut)
                    : FaultResult(exception, cancellationToken);
                Raise(sessionId, callId, providerId, toolName, ResultKind(result), target);
                return result;
            }
#pragma warning restore CA1031

            kinds = Kinds(result);
            result = CoworkResultLimiter.Apply(result);
            if (CarriesUntrusted(result))
            {
                lease.Taint();
            }

            Raise(sessionId, callId, providerId, toolName, result!.IsError ? CoworkToolEventKind.Failed : CoworkToolEventKind.Completed, target);
            return result;
        }
        finally
        {
            try
            {
                await _audit.RecordAsync(
                    new CoworkToolAuditEntry(
                        sessionId,
                        callId,
                        providerId,
                        toolName,
                        lease.Origin,
                        policy,
                        approval,
                        result?.IsError ?? true,
                        kinds,
                        started.Elapsed,
                        target),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _ = exception;
            }

            _ = lease.Call.Release();
        }
    }
#pragma warning restore MA0051

    private async ValueTask<CoworkApprovalOutcome> AskAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ApprovalTimeout);
#pragma warning disable CA1031 // A broker fault is a denial the model reads as not approved.
        try
        {
            var decision = await _broker.RequestAsync(request, timeout.Token).ConfigureAwait(false);
            return decision.Outcome;
        }
        catch (Exception exception) when (IsProviderFault(exception))
        {
            if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return CoworkApprovalOutcome.TimedOut;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return CoworkApprovalOutcome.Denied;
            }

            LogFault(request.ToolName, exception);
            return CoworkApprovalOutcome.Denied;
        }
#pragma warning restore CA1031
    }

    private bool TryGetLease(string sessionId, out SessionLease lease)
    {
        lock (_sessions)
        {
            return _sessions.TryGetValue(sessionId, out lease!);
        }
    }

    private bool Admits(string providerId) =>
        _options.AdmittedProviders is null || _options.AdmittedProviders.Contains(providerId);

    private ICoworkToolProvider ProviderFor(string providerId)
    {
        foreach (var provider in _providers)
        {
            if (string.Equals(provider.ProviderId, providerId, StringComparison.Ordinal))
            {
                return provider;
            }
        }

        throw new InvalidOperationException("The offered tool has no provider.");
    }

    private void Raise(string sessionId, string callId, string providerId, string toolName, CoworkToolEventKind kind, string? target)
    {
        ToolEvent?.Invoke(this, new CoworkToolEvent(sessionId, callId, providerId, toolName, kind, target));
    }

    private void LogFault(string toolName, Exception exception)
    {
        if (_logger is not null)
        {
            LogToolFault(_logger, toolName, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cowork tool {Tool} failed with {ExceptionType}.")]
    private static partial void LogToolFault(ILogger logger, string tool, string exceptionType);

    private static CoworkToolResult FaultResult(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return CoworkToolResult.Failure(CoworkToolMessages.Cancelled);
        }

        return CoworkToolResult.Failure(CoworkToolMessages.ProviderFailed);
    }

    private static CoworkToolEventKind ResultKind(CoworkToolResult result) =>
        result.IsError ? CoworkToolEventKind.Failed : CoworkToolEventKind.Completed;

    private static bool CarriesUntrusted(CoworkToolResult result)
    {
        foreach (var block in result.Content)
        {
            if (block is CoworkTextBlock text && text.IsUntrusted)
            {
                return true;
            }

            if (block is CoworkImageBlock image && image.IsUntrusted)
            {
                return true;
            }
        }

        return false;
    }

    private static CoworkResultKinds Kinds(CoworkToolResult result)
    {
        CoworkResultKinds kinds = CoworkResultKinds.Text;
        foreach (var block in result.Content)
        {
            if (block is CoworkTextBlock)
            {
                kinds |= CoworkResultKinds.Text;
            }
            else if (block is CoworkImageBlock)
            {
                kinds |= CoworkResultKinds.Image;
            }
        }

        return kinds;
    }

    private static int Utf8Bytes(JsonElement arguments) =>
        System.Text.Encoding.UTF8.GetByteCount(arguments.GetRawText());

    private static bool IsProviderFault(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;

    private void Remove(string sessionId)
    {
        lock (_sessions)
        {
            _ = _sessions.Remove(sessionId);
        }
    }

    private sealed class SessionLease : ICoworkSessionLease
    {
        private readonly CoworkToolHost _host;
        private int _disposed;

        internal SessionLease(CoworkToolHost host, CoworkSessionContext session)
        {
            _host = host;
            Session = session;
        }

        internal CoworkSessionContext Session { get; }

        internal SemaphoreSlim Call { get; } = new(1, 1);

        internal CoworkTurnOrigin Origin { get; private set; } = CoworkTurnOrigin.Local;

        public bool IsTainted { get; private set; }

        public void SetTurnOrigin(CoworkTurnOrigin origin) => Origin = origin;

        internal void Taint() => IsTainted = true;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _host.Remove(Session.SessionId);
            foreach (var provider in _host._providers)
            {
#pragma warning disable CA1031 // Ending a session must not surface a provider fault to the chat core.
                try
                {
                    await provider.EndSessionAsync(Session.SessionId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsProviderFault(exception))
                {
                    _host.LogFault(provider.ProviderId, exception);
                }
#pragma warning restore CA1031
            }

            Call.Dispose();
        }
    }
}
