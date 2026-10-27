using System.Buffers;
using System.Collections.Frozen;
using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Merges the result objects that <c>omp</c> section hooks print. Later hooks override earlier ones key by key, except that
/// the control flags <c>block</c>, <c>cancel</c>, <c>handled</c> and <c>continue</c> are sticky once true: one hook that blocks
/// cannot be undone by a later hook that does not.
/// </summary>
internal sealed class HookOmpResultMerger
{
    private static readonly FrozenSet<string> StickyFlags = new[] { "block", "cancel", "handled", "continue" }
        .ToFrozenSet(StringComparer.Ordinal);

    private readonly List<KeyValuePair<string, JsonElement>> _entries = [];

    /// <summary>True when at least one hook contributed a property.</summary>
    public bool HasValue => _entries.Count > 0;

    /// <summary>
    /// Adds one hook's result object.
    /// </summary>
    /// <param name="result">The object the hook printed.</param>
    public void Add(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in result.EnumerateObject())
        {
            Set(property.Name, property.Value.Clone());
        }
    }

    /// <summary>
    /// Adds a boolean control flag such as <c>block</c> that a synthetic deny sets.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    public void SetFlag(string name, bool value)
    {
        using var document = JsonDocument.Parse(value ? "true" : "false");
        Set(name, document.RootElement.Clone());
    }

    /// <summary>
    /// Adds a string property such as <c>reason</c>.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value.</param>
    public void SetText(string name, string value)
    {
        var buffer = new ArrayBufferWriter<byte>(64);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        Set(name, document.RootElement.Clone());
    }

    /// <summary>
    /// Returns the merged object, or null when nothing was added.
    /// </summary>
    /// <returns>A standalone JSON object.</returns>
    public JsonElement? Build()
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var entry in _entries)
            {
                writer.WritePropertyName(entry.Key);
                entry.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private void Set(string name, JsonElement value)
    {
        var index = _entries.FindIndex(entry => string.Equals(entry.Key, name, StringComparison.Ordinal));
        if (index < 0)
        {
            _entries.Add(new KeyValuePair<string, JsonElement>(name, value));
            return;
        }

        if (StickyFlags.Contains(name) && _entries[index].Value.ValueKind == JsonValueKind.True)
        {
            return;
        }

        _entries[index] = new KeyValuePair<string, JsonElement>(name, value);
    }
}
