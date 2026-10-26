namespace OmpOS.Harness.Acp;

/// <summary>
/// One select-style session configuration option the agent reported in <c>configOptions</c>, for example <c>model</c>, <c>mode</c> or
/// <c>thinking</c>. Options of another kind are not modelled and are left out of the parsed list.
/// </summary>
/// <param name="Id">The option identifier, for example <c>model</c>.</param>
/// <param name="Name">The display name the agent gave the option, or <see langword="null"/>.</param>
/// <param name="Category">The category the agent gave the option (<c>model</c>, <c>mode</c>, <c>thought_level</c>), or <see langword="null"/>.</param>
/// <param name="CurrentValue">The value the option has now.</param>
/// <param name="Values">The values the option can take, in the agent's order. Grouped values are flattened.</param>
public sealed record AcpConfigOption(string Id, string? Name, string? Category, string CurrentValue, IReadOnlyList<AcpConfigOptionValue> Values);
