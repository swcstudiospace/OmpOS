namespace Autonogrammer.Presentation.Cowork;

/// <summary>The three states a provider chip can show.</summary>
public enum ProviderChipState
{
    /// <summary>The provider can be used.</summary>
    Available,

    /// <summary>The provider needs setup before it can be used.</summary>
    NeedsSetup,

    /// <summary>The provider cannot be used. <see cref="ProviderStatusChip.Reason"/> says why.</summary>
    Unavailable,
}
