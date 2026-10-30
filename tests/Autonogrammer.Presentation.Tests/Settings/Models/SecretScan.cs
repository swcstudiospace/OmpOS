using System.Collections;
using System.Reflection;
using Shouldly;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>Walks the public surface of a model and fails when a string on it holds a secret.</summary>
internal static class SecretScan
{
    private const int MaxDepth = 8;

    /// <summary>Asserts that no public property reachable from <paramref name="root"/>, and no <c>ToString</c> on the way, holds <paramref name="secret"/>.</summary>
    public static void ShouldNotHold(object root, string secret)
    {
        ArgumentNullException.ThrowIfNull(root);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(root, secret, root.GetType().Name, seen, 0);
    }

    private static void Walk(object? value, string secret, string path, HashSet<object> seen, int depth)
    {
        if (value is null || depth > MaxDepth)
        {
            return;
        }

        if (value is string text)
        {
            text.Contains(secret, StringComparison.Ordinal).ShouldBeFalse($"{path} holds the secret");
            return;
        }

        var type = value.GetType();
        var isSystem = type.Namespace?.StartsWith("System", StringComparison.Ordinal) ?? false;
        if (type.IsEnum || (isSystem && value is not IEnumerable) || !seen.Add(value))
        {
            return;
        }

        (value.ToString() ?? string.Empty).Contains(secret, StringComparison.Ordinal).ShouldBeFalse($"{path}.ToString() holds the secret");
        if (value is IEnumerable items)
        {
            var index = 0;
            foreach (var item in items)
            {
                Walk(item, secret, $"{path}[{index++}]", seen, depth + 1);
            }

            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                Walk(property.GetValue(value), secret, $"{path}.{property.Name}", seen, depth + 1);
            }
        }
    }
}
