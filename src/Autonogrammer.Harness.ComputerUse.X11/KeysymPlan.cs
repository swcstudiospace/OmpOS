namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>How one character is injected. Scratch use always records a restore after <c>XSync</c>.</summary>
/// <param name="Keysym">The keysym to send.</param>
/// <param name="UsesScratchKeycode"><see langword="true"/> when no keycode already maps the keysym.</param>
/// <param name="RestoreAfterXSync"><see langword="true"/> when the scratch mapping must be restored after <c>XSync</c>.</param>
public readonly record struct KeysymPlan(uint Keysym, bool UsesScratchKeycode, bool RestoreAfterXSync);
