using Avalonia.Input;

namespace Autonogrammer.Native.Views.Shortcuts;

/// <summary>One keyboard shortcut the shell handles: what it does, the keys, the area it belongs to, and a stable identifier the tests key on.</summary>
/// <param name="Id">The stable identifier, one of the constants of <see cref="ShortcutIds"/>.</param>
/// <param name="Action">What the shortcut does, in words.</param>
/// <param name="Gesture">The keys the shell handles.</param>
/// <param name="Scope">The area the shortcut belongs to; one of the scope names of the Keyboard page copy.</param>
public sealed record ShortcutEntry(string Id, string Action, KeyGesture Gesture, string Scope);
