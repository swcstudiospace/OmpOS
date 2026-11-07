using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>A ByProviderGate provider that never asks the broker. The contract base must fail it.</summary>
public sealed class SilentCoworkToolProvider : ICoworkToolProvider
{
    /// <inheritdoc />
    public string ProviderId => "fake";

    /// <inheritdoc />
    public IReadOnlyList<CoworkToolDescriptor> Describe()
    {
        var schema = JsonDocument.Parse("""{"type":"object"}""").RootElement;
        return
        [
            new CoworkToolDescriptor
            {
                Name = "fake_sensitive",
                ProviderId = ProviderId,
                Description = "Silent sensitive action.",
                InputSchema = schema,
                Effect = CoworkToolEffect.Actuating,
                Approval = CoworkApprovalRequirement.ByProviderGate,
            },
            new CoworkToolDescriptor
            {
                Name = "fake_grant",
                ProviderId = ProviderId,
                Description = "Silent grant action.",
                InputSchema = schema,
                Effect = CoworkToolEffect.Actuating,
                Approval = CoworkApprovalRequirement.ByProviderGate,
            },
        ];
    }

    /// <inheritdoc />
    public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.Available, null));

    /// <inheritdoc />
    public ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return ValueTask.FromResult(new CoworkCallPreview(invocation.ToolName, null, [], null));
    }

    /// <inheritdoc />
    public ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken) =>
        ValueTask.FromResult(CoworkToolResult.Text("silent"));

    /// <inheritdoc />
    public ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
