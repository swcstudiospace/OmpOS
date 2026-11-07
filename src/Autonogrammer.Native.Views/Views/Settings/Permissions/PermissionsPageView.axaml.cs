#pragma warning disable MA0048 // The plan owns this file for the model and its related types.
using Autonogrammer.Presentation.Settings.Permissions;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>The values the Permissions view binds. Wiring into the settings window belongs to a later plan.</summary>
public sealed class PermissionsPageState
{
    /// <summary>Initializes a new instance of the <see cref="PermissionsPageState"/> class.</summary>
    /// <param name="modes">The mode picker.</param>
    /// <param name="grants">The folder grants.</param>
    /// <param name="statements">The fixed sentences.</param>
    public PermissionsPageState(PermissionModesModel modes, FolderGrantModel grants, PermissionStatementModel statements)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(statements);
        SelectedMode = modes.SelectedLabel;
        ModeLabels = modes.ModeLabels;
        SkipWarning = PermissionModesModel.SkipWarning;
        Grants = grants.Grants.Select(grant => grant.Path + " " + grant.Access + " " + grant.Scope).ToArray();
        Statements = statements.Statements;
    }

    /// <summary>Gets the selected mode label.</summary>
    public string SelectedMode { get; }

    /// <summary>Gets the mode labels.</summary>
    public IReadOnlyList<string> ModeLabels { get; }

    /// <summary>Gets the Skip warning.</summary>
    public string SkipWarning { get; }

    /// <summary>Gets the folder grant lines.</summary>
    public IReadOnlyList<string> Grants { get; }

    /// <summary>Gets the limit sentences.</summary>
    public IReadOnlyList<string> Statements { get; }
}

/// <summary>The Permissions settings view. It binds Presentation models only.</summary>
public partial class PermissionsPageView : UserControl
{
    /// <summary>Creates an empty view.</summary>
    public PermissionsPageView()
    {
        InitializeComponent();
    }

    /// <summary>Creates a view bound to the models.</summary>
    /// <param name="modes">The mode picker.</param>
    /// <param name="grants">The folder grants.</param>
    /// <param name="statements">The fixed sentences.</param>
    public PermissionsPageView(PermissionModesModel modes, FolderGrantModel grants, PermissionStatementModel statements)
        : this()
    {
        DataContext = new PermissionsPageState(modes, grants, statements);
    }
}
