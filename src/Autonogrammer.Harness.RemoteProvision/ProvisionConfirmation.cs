namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The user's confirmation that these exact installs may run on one environment. It records the action list the user was shown, so a later
/// plan with any other action (a new version, another plugin commit) needs a new confirmation before the first write.
/// </summary>
/// <param name="EnvironmentId">The environment the confirmation is for.</param>
/// <param name="Actions">The action list shown to the user, in order, exactly as <see cref="ProvisionNeedsConfirmation.Actions"/> had it.</param>
/// <param name="ConfirmedAt">When the user confirmed, on the caller's clock.</param>
public sealed record ProvisionConfirmation(string EnvironmentId, IReadOnlyList<string> Actions, DateTimeOffset ConfirmedAt);
