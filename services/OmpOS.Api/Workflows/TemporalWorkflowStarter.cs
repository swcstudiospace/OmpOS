using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Workflows;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Starts the session-run workflow through the Temporal client. The client is created with
/// <see cref="TemporalClient.CreateLazy"/>, so the API does not dial Temporal at startup; the first start connects,
/// and a failed connection is retried on the next start.
/// </summary>
internal sealed class TemporalWorkflowStarter : ITemporalWorkflowStarter
{
    /// <summary>
    /// The workflow type name the worker registers. The worker's <c>TemporalSessionRunWorkflow</c> class carries
    /// no explicit <c>[Workflow]</c> name, so Temporal uses the class name.
    /// </summary>
    internal const string WorkflowTypeName = "TemporalSessionRunWorkflow";

    private readonly TemporalClient _client;

    /// <summary>Initializes a new instance of the <see cref="TemporalWorkflowStarter"/> class.</summary>
    /// <param name="options">The workflows options carrying the Temporal client settings.</param>
    public TemporalWorkflowStarter(IOptions<WorkflowsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _client = TemporalClient.CreateLazy(BuildConnectOptions(options.Value));
    }

    /// <inheritdoc />
    public async Task<string?> StartAsync(string workflowId, string taskQueue, SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskQueue);
        ArgumentNullException.ThrowIfNull(input);

        var options = new WorkflowOptions(workflowId, taskQueue)
        {
            // A running run with this id is returned instead of started twice; a closed run keeps its id reserved.
            IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
            IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
            Rpc = new RpcOptions { CancellationToken = cancellationToken },
        };
        try
        {
            var handle = await _client
                .StartWorkflowAsync(WorkflowTypeName, new object[] { input }, options)
                .ConfigureAwait(false);
            return handle.FirstExecutionRunId;
        }
        catch (WorkflowAlreadyStartedException existing)
        {
            // The idempotency key already owns a closed run: report that run rather than start a duplicate.
            return existing.RunId;
        }
    }

    /// <summary>Maps the workflows options onto Temporal client connect options.</summary>
    /// <param name="options">The workflows options.</param>
    /// <returns>The connect options. The API key is set only when non-blank and is never logged.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="WorkflowsOptions.Address"/> is unset.</exception>
    internal static TemporalClientConnectOptions BuildConnectOptions(WorkflowsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var address = options.Address
            ?? throw new InvalidOperationException("Workflows:Address is required when Workflows:Enabled is true.");
        var connect = new TemporalClientConnectOptions(address)
        {
            Namespace = options.Namespace,
            DataConverter = SessionRunDataConverter.Instance,
        };

        var apiKey = options.ApiKey?.Trim();
        if (!string.IsNullOrEmpty(apiKey))
        {
            connect.ApiKey = apiKey;
        }

        if (options.Tls)
        {
            connect.Tls = new TlsOptions();
        }

        return connect;
    }
}
