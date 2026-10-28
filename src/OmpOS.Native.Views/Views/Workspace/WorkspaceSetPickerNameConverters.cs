using Avalonia.Data.Converters;

namespace OmpOS.Native.Views.Workspace;

/// <summary>Converters of the workspace set picker rows: the accessible names of the per-row buttons, so each one names the folder it acts on, and the empty-list test.</summary>
internal static class WorkspaceSetPickerNameConverters
{
    /// <summary>Converts a folder name to the accessible name of its Make primary button.</summary>
    public static readonly FuncValueConverter<string?, string> MakePrimary = new(static name => WorkspaceSetPickerCopy.MakePrimaryName(name ?? string.Empty));

    /// <summary>Converts a folder name to the accessible name of its Remove button.</summary>
    public static readonly FuncValueConverter<string?, string> Remove = new(static name => WorkspaceSetPickerCopy.RemoveName(name ?? string.Empty));

    /// <summary>Converts the error flag of a finding to the word that opens it: Error or Warning.</summary>
    public static readonly FuncValueConverter<bool, string> Severity = new(static isError => isError ? WorkspaceSetPickerCopy.ErrorPrefix : WorkspaceSetPickerCopy.WarningPrefix);

    /// <summary>Converts a count to true when it is zero.</summary>
    public static readonly FuncValueConverter<int, bool> IsZero = new(static count => count == 0);
}
