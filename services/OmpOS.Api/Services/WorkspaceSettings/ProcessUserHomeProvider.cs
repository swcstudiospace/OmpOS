namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>The home directory of the user the API process runs as.</summary>
internal sealed class ProcessUserHomeProvider : IUserHomeProvider
{
    /// <inheritdoc />
    public string? Home
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(home) ? null : home;
        }
    }
}
