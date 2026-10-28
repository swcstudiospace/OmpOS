namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>Which product's folder a permission or sandbox file is in.</summary>
public enum PermissionFileOrigin
{
    /// <summary>An Autonogrammer file under <c>.autonogrammer</c>. It is the only kind Autonogrammer writes.</summary>
    Autonogrammer = 0,

    /// <summary>A Cursor file under <c>.cursor</c>. Autonogrammer only reads it.</summary>
    Cursor = 1,
}
