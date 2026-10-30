namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>A value the user supplies for a base URL placeholder, for example the product id of an account or the zone of a deployment.</summary>
/// <param name="Name">The placeholder name used in the base URL template.</param>
/// <param name="Label">The label of the input.</param>
/// <param name="Example">An example value, or null.</param>
/// <param name="Help">A sentence that says where the user finds the value, or null.</param>
public sealed record ProviderListingVariable(string Name, string Label, string? Example, string? Help);
