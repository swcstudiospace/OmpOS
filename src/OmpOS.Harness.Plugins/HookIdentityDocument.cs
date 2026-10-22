namespace OmpOS.Harness.Plugins;

/// <summary>
/// The canonical form that a hook's consent hash is computed over.
/// </summary>
/// <remarks>
/// Field order is part of the hash. The serializer writes properties in declaration order, so the hashed fields are, in
/// order: 1. <c>Arguments</c>, 2. <c>WorkingDirectory</c>, 3. <c>Trigger</c>, 4. <c>Blocking</c>. Reordering, renaming
/// or adding a field changes every digest and makes existing consent void.
/// </remarks>
internal sealed class HookIdentityDocument
{
    // 1. The argument list, in order. The first element is the executable.
    public List<string> Arguments { get; set; } = [];

    // 2. The absolute directory the process starts in.
    public string WorkingDirectory { get; set; } = string.Empty;

    // 3. The lifecycle event, as its enum name, for example "BeforeAction".
    public string Trigger { get; set; } = string.Empty;

    // 4. Whether the hook may deny the action.
    public bool Blocking { get; set; }
}
