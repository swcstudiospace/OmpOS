namespace OmpOS.Harness.Plugins;

/// <summary>
/// The exact command a hook runs. Consent is recorded against the identity of this value, together with the hook's
/// trigger and blocking flag, so any change to them needs new consent.
/// </summary>
/// <param name="Arguments">The argument list. The first element is the executable; no shell is involved.</param>
/// <param name="WorkingDirectory">The absolute directory the process starts in.</param>
public sealed record HookCommand(IReadOnlyList<string> Arguments, string WorkingDirectory);
