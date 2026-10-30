namespace Autonogrammer.Presentation.Cloud;

/// <summary>One entry of the status filter: all statuses, or one.</summary>
/// <param name="Status">The status the entry keeps, or null for every status.</param>
/// <param name="Label">The words of the entry.</param>
public sealed record CloudFilterOption(CloudAgentStatus? Status, string Label);
