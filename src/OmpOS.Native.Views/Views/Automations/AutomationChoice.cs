namespace OmpOS.Native.Views.Automations;

/// <summary>One entry of a pick list of the automation editor: the value the model stores and the label the user reads.</summary>
/// <param name="Value">The value the model stores.</param>
/// <param name="Label">The label the user reads.</param>
internal sealed record AutomationChoice(string Value, string Label);
