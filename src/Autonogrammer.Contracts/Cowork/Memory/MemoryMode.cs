namespace Autonogrammer.Contracts.Cowork.Memory;

/// <summary>
/// Whether memory is on for a session. Fixed at session start. Off removes the provider and injects no brief.
/// Cowork defaults to <see cref="On"/> when the account has memory enabled. Code defaults to <see cref="Off"/>.
/// </summary>
public enum MemoryMode
{
    /// <summary>Memory tools and the brief are available when the account has memory enabled.</summary>
    On = 0,

    /// <summary>Memory is off for this task. The provider is removed and no brief is injected.</summary>
    Off = 1,
}
