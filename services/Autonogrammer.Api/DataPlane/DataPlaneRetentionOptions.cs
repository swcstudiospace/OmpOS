namespace Autonogrammer.Api.DataPlane;

/// <summary>Binds <c>DataPlane:Retention</c>: how long dispatch and audit rows are kept, in days.</summary>
internal sealed class DataPlaneRetentionOptions
{
    /// <summary>Gets the audit retention below which the API refuses to start, in days.</summary>
    public const int AuditFloor = 90;

    /// <summary>Gets or sets the retention of dispatch commands, in days. The default is 30.</summary>
    public int DispatchDays { get; set; } = 30;

    /// <summary>Gets or sets the retention of audit rows, in days. The default is 400.</summary>
    public int AuditDays { get; set; } = 400;

    /// <summary>Gets or sets the least audit retention the deployment accepts, in days. It cannot be set below 90; the default is 90.</summary>
    public int AuditFloorDays { get; set; } = AuditFloor;
}
