namespace OmpOS.Presentation.Settings.Models;

/// <summary>One tier of a coding plan with its published request limits, already worded for the user.</summary>
/// <param name="Name">The tier name as the provider publishes it.</param>
/// <param name="QuotaText">The published limits, for example "about 600 requests per 5 hours". Empty when the provider publishes none.</param>
public sealed record PlanTierInfo(string Name, string QuotaText);
