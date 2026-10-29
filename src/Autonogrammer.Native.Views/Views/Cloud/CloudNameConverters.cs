using Autonogrammer.Presentation.Cloud;
using Avalonia.Data.Converters;

namespace Autonogrammer.Native.Views.Cloud;

/// <summary>Builds the accessible names of the buttons inside the new-agent form's lists, so each Remove button names what it removes.</summary>
internal static class CloudNameConverters
{
    /// <summary>Converts a repository name to the accessible name of its Remove button.</summary>
    public static readonly FuncValueConverter<string?, string> RemoveRepository = new(static name => CloudCopy.RemoveRepositoryName(name ?? string.Empty));

    /// <summary>Converts a secret name to the accessible name of its Remove button.</summary>
    public static readonly FuncValueConverter<string?, string> RemoveSecret = new(static name => CloudCopy.RemoveSecretName(name ?? string.Empty));
}
