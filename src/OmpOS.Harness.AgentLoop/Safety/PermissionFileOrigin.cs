namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>Which product's folder a permission or sandbox file is in.</summary>
public enum PermissionFileOrigin
{
    /// <summary>An OmpOS file under <c>.ompos</c>. It is the only kind OmpOS writes.</summary>
    Ompos = 0,

    /// <summary>A Cursor file under <c>.cursor</c>. OmpOS only reads it.</summary>
    Cursor = 1,
}
