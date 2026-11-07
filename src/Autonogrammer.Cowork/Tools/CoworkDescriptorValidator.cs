using System.Text.Json;
using System.Text.RegularExpressions;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>Enforces the descriptor rules before a tool is offered.</summary>
public static partial class CoworkDescriptorValidator
{
    /// <summary>The longest tool name, in characters.</summary>
    public const int MaxNameLength = 40;

    /// <summary>The shortest tool name, in characters.</summary>
    public const int MinNameLength = 3;

    /// <summary>The longest description, in characters.</summary>
    public const int MaxDescriptionLength = 1024;

    /// <summary>
    /// Checks one descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="reason">The first rule it breaks, or null when it is valid.</param>
    /// <returns>True when every rule holds.</returns>
    public static bool TryValidate(CoworkToolDescriptor descriptor, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrEmpty(descriptor.ProviderId) || !ProviderIdPattern().IsMatch(descriptor.ProviderId))
        {
            reason = "ProviderId is not a lowercase identifier of 2 to 20 characters.";
            return false;
        }

        if (string.IsNullOrEmpty(descriptor.Name)
            || descriptor.Name.Length < MinNameLength
            || descriptor.Name.Length > MaxNameLength
            || !NamePattern().IsMatch(descriptor.Name)
            || descriptor.Name.Contains("__", StringComparison.Ordinal)
            || descriptor.Name.StartsWith("cowork_", StringComparison.Ordinal)
            || !descriptor.Name.StartsWith(descriptor.ProviderId + "_", StringComparison.Ordinal))
        {
            reason = "Name must be 3 to 40 characters, start with the provider id and an underscore, and contain no double underscore or cowork_ prefix.";
            return false;
        }

        if (descriptor.Description is null || descriptor.Description.Length > MaxDescriptionLength)
        {
            reason = "Description is longer than 1024 characters.";
            return false;
        }

        if (descriptor.InputSchema.ValueKind != JsonValueKind.Object)
        {
            reason = "InputSchema must be a JSON object.";
            return false;
        }

        if (descriptor.Approval == CoworkApprovalRequirement.ByProviderGate && descriptor.Effect == CoworkToolEffect.Destructive)
        {
            reason = "ByProviderGate is refused on a destructive tool.";
            return false;
        }

        reason = null;
        return true;
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z][a-z0-9]{1,19}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex ProviderIdPattern();
}
