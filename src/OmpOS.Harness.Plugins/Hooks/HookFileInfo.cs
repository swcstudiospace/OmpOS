namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// What a <see cref="IHookFileSource"/> knows about a candidate file without reading it.
/// </summary>
/// <param name="Exists">True when a regular file exists at the path.</param>
/// <param name="Length">The size in bytes; zero when the file does not exist.</param>
/// <param name="IsTamperResistant">
/// True when neither the file nor its directory is writable by group or others. A managed file counts as pre-consented
/// only when this is true. It is false on Windows, where OmpOS does not read access control lists.
/// </param>
public sealed record HookFileInfo(bool Exists, long Length, bool IsTamperResistant);
