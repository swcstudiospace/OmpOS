namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// One reason an automation definition is not acceptable. The message never repeats a value the user typed.
/// </summary>
/// <param name="Field">The field it concerns: <c>name</c>, <c>prompt</c>, <c>triggers</c>, <c>cron</c>, <c>webhook</c>, <c>target</c>, <c>repositories</c>, <c>secrets</c>, <c>tools</c>, <c>timeZone</c> or <c>limits</c>.</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record AutomationValidationIssue(string Field, string Message);
