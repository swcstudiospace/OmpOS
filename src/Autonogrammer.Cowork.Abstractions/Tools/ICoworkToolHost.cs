using System.Text.Json;

namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The seam the gateway serves. Providers register here and never talk to omp.</summary>
public interface ICoworkToolHost
{
    /// <summary>Opens a session slot.</summary>
    /// <param name="session">The session facts.</param>
    /// <returns>The lease the chat core keeps for the session.</returns>
    ICoworkSessionLease BeginSession(CoworkSessionContext session);

#pragma warning disable CA1003 // ADR-0032 D5 keeps CoworkToolEvent a record, which cannot derive from EventArgs.
#pragma warning disable MA0046 // The same record cannot derive from EventArgs, so the event payload stays the D5 record.
    /// <summary>Raised on the caller's context for started, approval requested, completed, failed and denied.</summary>
    event EventHandler<CoworkToolEvent>? ToolEvent;
#pragma warning restore MA0046
#pragma warning restore CA1003

    /// <summary>Lists the tools offered to the session right now.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the list.</param>
    /// <returns>The offered descriptors.</returns>
    ValueTask<IReadOnlyList<CoworkToolDescriptor>> ListOfferedAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Runs one call through the eight-step sequence.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="toolName">The tool name.</param>
    /// <param name="arguments">The model-supplied JSON arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result the model reads, including fixed-text failures.</returns>
    ValueTask<CoworkToolResult> InvokeAsync(string sessionId, string toolName, JsonElement arguments, CancellationToken cancellationToken);
}
