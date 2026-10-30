using Autonogrammer.Contracts.Controls;

namespace Autonogrammer.Api.Services;

/// <summary>What a live engine session last reported for a session: its options and its slash commands.</summary>
/// <param name="Options">The options, in the engine's order.</param>
/// <param name="Commands">The commands, in the engine's order.</param>
internal sealed record ControlSnapshot(IReadOnlyList<ConfigOption> Options, IReadOnlyList<SessionCommand> Commands);
