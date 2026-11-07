using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The approvals waiting on a Cowork conversation. Each card keeps the contract
/// <see cref="PendingToolApproval"/> and the host-observed target and details. Model text is stored so a test can
/// prove it is never the instruction a view renders.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "D-28 names this CoworkApprovalQueue. It is not a System.Collections queue.")]
public sealed class CoworkApprovalQueue : ObservableModel
{
    private readonly List<CoworkApprovalCardModel> _cards = [];

    /// <summary>Gets the cards, oldest first.</summary>
    public IReadOnlyList<CoworkApprovalCardModel> Cards => _cards;

    /// <summary>Adds one request. The target and details are host-observed and are what a card renders.</summary>
    /// <param name="approval">The contract record.</param>
    /// <param name="target">The host-observed target.</param>
    /// <param name="details">The host-observed details.</param>
    /// <param name="ignoredModelText">Model text that must not be rendered as an instruction. Null when the request carried none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="approval"/> is null.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="details"/> is null.</exception>
    public void Add(PendingToolApproval approval, string target, string details, string? ignoredModelText = null)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(details);
        _cards.Add(new CoworkApprovalCardModel(approval, target, details, ignoredModelText));
        NotifyPropertyChanged(nameof(Cards));
    }
}
