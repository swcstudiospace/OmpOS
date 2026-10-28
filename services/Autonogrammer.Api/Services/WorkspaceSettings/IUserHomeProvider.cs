namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Names the home directory of the API process. Tests replace it so no test reads the real home.</summary>
internal interface IUserHomeProvider
{
    /// <summary>Gets the absolute home directory, or null when the process has none.</summary>
    string? Home { get; }
}
