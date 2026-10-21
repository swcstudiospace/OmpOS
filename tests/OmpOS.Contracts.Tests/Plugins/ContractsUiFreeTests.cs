using System.Reflection;
using OmpOS.Contracts.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Plugins;

/// <summary>
/// Verifies that the contracts assembly stays UI-free (XP-09, ROADMAP criterion 7) and that every
/// plugin contribution contract is a sealed record with required members.
/// </summary>
public sealed class ContractsUiFreeTests
{
    private const string RequiredMemberAttributeName = "System.Runtime.CompilerServices.RequiredMemberAttribute";

    private static readonly string[] UiFrameworkPrefixes = ["Avalonia", "Microsoft.Maui", "Microsoft.AspNetCore.Components"];

    /// <summary>XP-09: OmpOS.Contracts references no Avalonia, MAUI or ASP.NET Core Components assembly.</summary>
    [Fact]
    public void ContractsAssembly_ReferencesNoUiFrameworkAssembly()
    {
        var referenced = typeof(AssemblyMarker).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        referenced.Where(IsUiFrameworkAssembly).ShouldBeEmpty("OmpOS.Contracts must not reference a UI framework assembly");
    }

    /// <summary>XP-09: each plugin contract exists in OmpOS.Contracts.Plugins as a sealed record with required members.</summary>
    [Theory]
    [InlineData(typeof(PluginDescriptor))]
    [InlineData(typeof(CommandContribution))]
    [InlineData(typeof(PanelContribution))]
    [InlineData(typeof(ToolContribution))]
    [InlineData(typeof(AgentModeContribution))]
    public void PluginContract_IsSealedRecordWithRequiredMembers(Type contract)
    {
        contract.Namespace.ShouldBe("OmpOS.Contracts.Plugins");
        contract.IsSealed.ShouldBeTrue();
        ImplementsRecordEquality(contract).ShouldBeTrue();
        HasRequiredMembers(contract).ShouldBeTrue();
    }

    private static bool IsUiFrameworkAssembly(string assemblyName)
    {
        return UiFrameworkPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool ImplementsRecordEquality(Type contract)
    {
        return contract.GetInterfaces().Any(
            implemented => implemented.IsGenericType
                && implemented.GetGenericTypeDefinition() == typeof(IEquatable<>)
                && implemented.GenericTypeArguments[0] == contract);
    }

    private static bool HasRequiredMembers(Type contract)
    {
        return contract.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, RequiredMemberAttributeName, StringComparison.Ordinal))
            || contract.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, RequiredMemberAttributeName, StringComparison.Ordinal)));
    }
}
