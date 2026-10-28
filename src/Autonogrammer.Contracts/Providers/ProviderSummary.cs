using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>
/// One provider, aggregator, coding plan or local runtime of the catalogue, with whether the service has a key for it. A summary holds
/// no key: it names the environment variable that carries one.
/// </summary>
public sealed record ProviderSummary
{
    /// <summary>Gets the provider slug, unique in the catalogue.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown on the card.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the part of the world the provider belongs to.</summary>
    public required ProviderRegion Region { get; init; }

    /// <summary>Gets the ISO 3166-1 alpha-2 country of the vendor or host, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Country { get; init; }

    /// <summary>Gets what the vendor states about where data is processed, or null. Autonogrammer does not verify it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResidencyNote { get; init; }

    /// <summary>Gets what the provider sells or runs.</summary>
    public required ProviderKind Kind { get; init; }

    /// <summary>Gets how far the research verified the provider.</summary>
    public required ProviderVerification Status { get; init; }

    /// <summary>Gets the date the status was established.</summary>
    public required DateOnly StatusDate { get; init; }

    /// <summary>Gets the id omp knows the provider by, or null when omp has no native provider for it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OmpProviderId { get; init; }

    /// <summary>Gets the name of the environment variable that carries the key, or null. A name, never a value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKeyEnv { get; init; }

    /// <summary>Gets a value indicating whether the provider takes an API key.</summary>
    public required bool TakesKey { get; init; }

    /// <summary>Gets a value indicating whether the provider signs in through omp and takes no key.</summary>
    public required bool SignInOnly { get; init; }

    /// <summary>Gets the omp command that signs in for a sign-in provider, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LoginCommand { get; init; }

    /// <summary>Gets the ways to reach the provider; the first is the default.</summary>
    public required IReadOnlyList<ProviderRouteInfo> Routes { get; init; }

    /// <summary>Gets the placeholders the base URLs hold.</summary>
    public required IReadOnlyList<ProviderUrlVariableInfo> UrlVariables { get; init; }

    /// <summary>Gets the vendor documentation, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? DocsUrl { get; init; }

    /// <summary>Gets the page where a key is created, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? KeyPageUrl { get; init; }

    /// <summary>Gets the page the row was read on.</summary>
    public required Uri SourceUrl { get; init; }

    /// <summary>Gets a short caveat, or null. An unverified provider always has one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    /// <summary>Gets the plan terms of a coding plan, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProviderPlanInfo? Plan { get; init; }

    /// <summary>Gets whether the service has a key or sign-in for the provider.</summary>
    public required ProviderConfiguredState Configured { get; init; }
}
