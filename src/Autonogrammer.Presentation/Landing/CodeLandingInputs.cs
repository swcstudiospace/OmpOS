using Autonogrammer.Presentation.Accounts;

namespace Autonogrammer.Presentation.Landing;

/// <summary>What the Code landing draws from, as the window reads it.</summary>
/// <param name="Local">The status of omp on this computer.</param>
/// <param name="Remote">Whether the remote environments have been read.</param>
/// <param name="Account">The state of the account, or null when the window has no account host.</param>
/// <param name="LinkedMachines">The names of the machines linked to the account, or null when that list is not available in this window.</param>
/// <param name="LocalUpdate">The outcome of the check for a newer managed omp, or null when no check is wired or none has finished.</param>
public sealed record CodeLandingInputs(
    LocalOmpStatus Local,
    RemoteLoadState Remote,
    AccountHostState? Account,
    IReadOnlyList<string>? LinkedMachines,
    LocalOmpUpdate? LocalUpdate = null);
