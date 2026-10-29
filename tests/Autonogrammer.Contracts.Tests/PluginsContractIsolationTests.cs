using System.Reflection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests;

/// <summary>
/// Guards the ADR-0008 plugin contract family boundary: no type declared in
/// <c>Autonogrammer.Contracts.Plugins</c> may reference a <c>Autonogrammer.Contracts.Ui</c> type.
/// </summary>
public sealed class PluginsContractIsolationTests
{
    [Fact]
    public void PluginsNeverReferenceUi()
    {
        var contracts = typeof(ContractVersion).Assembly;
        var offending = new List<string>();

        foreach (var type in contracts.GetTypes().Where(static t => string.Equals(t.Namespace, "Autonogrammer.Contracts.Plugins", StringComparison.Ordinal)))
        {
            foreach (var referenced in WalkReferencedTypes(type))
            {
                if (string.Equals(referenced.Namespace, "Autonogrammer.Contracts.Ui", StringComparison.Ordinal))
                {
                    offending.Add($"{type.FullName} references {referenced.FullName}");
                }
            }
        }

        offending.ShouldBeEmpty();
    }

    private static IEnumerable<Type> WalkReferencedTypes(Type type)
    {
        var seen = new HashSet<Type>();
        var stack = new Stack<Type>();
        stack.Push(type);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            yield return current;

            foreach (var next in DirectReferences(current))
            {
                stack.Push(next);
            }
        }
    }

    private static IEnumerable<Type> DirectReferences(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var iface in type.GetInterfaces())
        {
            yield return iface;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return property.PropertyType;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName && (method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal)))
            {
                continue;
            }

            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var argument in type.GenericTypeArguments)
        {
            yield return argument;
        }
    }
}
