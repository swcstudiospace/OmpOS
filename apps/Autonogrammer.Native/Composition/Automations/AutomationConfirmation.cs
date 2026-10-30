namespace Autonogrammer.Native.Composition.Automations;

/// <summary>What the approval card of a first run says.</summary>
/// <param name="Title">The card's title.</param>
/// <param name="Summary">The sentence under it: the automation's name and what it will do.</param>
internal sealed record AutomationConfirmation(string Title, string Summary);
