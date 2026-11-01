using Autonogrammer.Contracts.Controls;

namespace Autonogrammer.Native.Cowork;

/// <summary>A card in the single conversation queue. Target and details are host-observed.</summary>
/// <param name="Approval">The contract card.</param>
/// <param name="Target">The host-observed target.</param>
/// <param name="Details">The host-observed details.</param>
/// <param name="StatusText">Timed out, Unavailable, or null while waiting.</param>
internal sealed record CoworkApprovalCard(PendingToolApproval Approval, string? Target, IReadOnlyList<string> Details, string? StatusText);
