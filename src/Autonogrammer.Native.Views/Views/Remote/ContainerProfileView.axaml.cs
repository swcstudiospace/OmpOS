using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Remote;

/// <summary>
/// The fields of a container environment: name, container, user, folder, podman and omp paths, and the problems of the current values. It draws a
/// <see cref="Autonogrammer.Presentation.Remote.ContainerProfileEditor"/>; the section that hosts it owns the Save and Cancel buttons.
/// </summary>
internal partial class ContainerProfileView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ContainerProfileView"/> class.</summary>
    public ContainerProfileView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the first field, which the section focuses when the form opens.</summary>
    internal TextBox FirstField => DisplayNameBox;

    /// <summary>Gets the container name field.</summary>
    internal TextBox ContainerField => ContainerBox;

    /// <summary>Gets the list of problems.</summary>
    internal ItemsControl Issues => IssueRows;
}
