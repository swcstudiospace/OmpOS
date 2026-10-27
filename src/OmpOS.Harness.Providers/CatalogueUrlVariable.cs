namespace OmpOS.Harness.Providers;

/// <summary>
/// A named placeholder in a route's base URL, written <c>{name}</c> in the template. The user supplies the value, for example the
/// account's product id or the region of a deployment.
/// </summary>
/// <param name="Name">The placeholder name: lower-case letters, digits and underscores, starting with a letter.</param>
/// <param name="Label">The label of the input in the settings form.</param>
/// <param name="Example">An example value, or null.</param>
/// <param name="Help">A short sentence that says where the user finds the value, or null.</param>
public sealed record CatalogueUrlVariable(string Name, string Label, string? Example = null, string? Help = null);
