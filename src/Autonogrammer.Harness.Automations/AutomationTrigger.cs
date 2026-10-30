namespace Autonogrammer.Harness.Automations;

/// <summary>
/// What an automation responds to. Manual runs are requested by a caller, scheduled runs fire when their schedule is due
/// on the injected clock, and event runs fire when an event with their name is dispatched.
/// </summary>
public abstract record AutomationTrigger;
