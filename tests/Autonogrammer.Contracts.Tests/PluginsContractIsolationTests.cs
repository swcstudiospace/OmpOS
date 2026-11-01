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
        var visited = 0;

        foreach (var type in contracts.GetTypes().Where(static t => string.Equals(t.Namespace, "Autonogrammer.Contracts.Plugins", StringComparison.Ordinal)))
        {
            visited++;
            foreach (var referenced in WalkReferencedTypes(type))
            {
                if (string.Equals(referenced.Namespace, "Autonogrammer.Contracts.Ui", StringComparison.Ordinal))
                {
                    offending.Add($"{type.FullName} references {referenced.FullName}");
                }
            }
        }

        visited.ShouldBeGreaterThanOrEqualTo(5, "the five Plugins contract types must all be visited");
        offending.ShouldBeEmpty();
    }

    [Fact]
    public void WalkDetectsUiReferences()
    {
        var uiType = typeof(ContractVersion).Assembly.GetTypes()
            .First(static t => string.Equals(t.Namespace, "Autonogrammer.Contracts.Ui", StringComparison.Ordinal));

        WalkReferencedTypes(uiType)
            .ShouldContain(static t => string.Equals(t.Namespace, "Autonogrammer.Contracts.Ui", StringComparison.Ordinal));
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

            var nexts = current.Assembly == typeof(ContractVersion).Assembly
                ? DirectReferences(current)
                : BoundaryReferences(current);

            foreach (var next in nexts)
            {
                stack.Push(next);
            }
        }
    }

    private static IEnumerable<Type> BoundaryReferences(Type type)
    {
        var element = type.GetElementType();
        if (element is not null)
        {
            yield return element;
        }

        foreach (var argument in type.GenericTypeArguments)
        {
            yield return argument;
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
