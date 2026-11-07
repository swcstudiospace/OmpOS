namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// One provider status chip. The label is a tested constant, never a colour, and Unavailable always carries a reason.
/// </summary>
public sealed class ProviderStatusChip
{
    /// <summary>The label for <see cref="ProviderChipState.Available"/>.</summary>
    public const string AvailableLabel = "Available";

    /// <summary>The label for <see cref="ProviderChipState.NeedsSetup"/>.</summary>
    public const string NeedsSetupLabel = "Needs setup";

    /// <summary>The label for <see cref="ProviderChipState.Unavailable"/>.</summary>
    public const string UnavailableLabel = "Unavailable";

    /// <summary>Creates a chip.</summary>
    /// <param name="name">The provider name spoken with the state.</param>
    /// <param name="state">The state.</param>
    /// <param name="reason">The reason. Required when <paramref name="state"/> is <see cref="ProviderChipState.Unavailable"/>.</param>
    /// <exception cref="ArgumentException">The name is blank, or Unavailable has no reason.</exception>
    public ProviderStatusChip(string name, ProviderChipState state, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (state == ProviderChipState.Unavailable && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("An unavailable provider needs a reason.", nameof(reason));
        }

        Name = name;
        State = state;
        Reason = state == ProviderChipState.Unavailable ? reason : null;
    }

    /// <summary>Gets the provider name.</summary>
    public string Name { get; }

    /// <summary>Gets the state.</summary>
    public ProviderChipState State { get; }

    /// <summary>Gets the reason when <see cref="State"/> is <see cref="ProviderChipState.Unavailable"/>; otherwise null.</summary>
    public string? Reason { get; }

    /// <summary>Gets the accessible name: the provider, then the state, then the reason when there is one.</summary>
    public string AccessibleName => Reason is null ? Name + ", " + Label : Name + ", " + Label + ", " + Reason;

    /// <summary>Gets the state label.</summary>
    public string Label => State switch
    {
        ProviderChipState.Available => AvailableLabel,
        ProviderChipState.NeedsSetup => NeedsSetupLabel,
        _ => UnavailableLabel,
    };
}
